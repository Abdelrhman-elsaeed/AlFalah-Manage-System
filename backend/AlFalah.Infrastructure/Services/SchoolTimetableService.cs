using System.Text.Json;
using AlFalah.Application.Common;
using AlFalah.Application.DTOs.Timetables;
using AlFalah.Application.Interfaces;
using AlFalah.Domain.Entities;
using AlFalah.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AlFalah.Infrastructure.Services;

public sealed class SchoolTimetableService : ISchoolTimetableService
{
    private const int PeriodCount = 8;
    private readonly ISchoolTimetableRepository _repository;
    private readonly ISchoolTimetableDocumentService _documents;
    private readonly ICurrentUserService _currentUser;
    private readonly SchoolScopeGuard _scopeGuard;

    public SchoolTimetableService(
        ISchoolTimetableRepository repository,
        ISchoolTimetableDocumentService documents,
        ICurrentUserService currentUser,
        SchoolScopeGuard scopeGuard)
    {
        _repository = repository;
        _documents = documents;
        _currentUser = currentUser;
        _scopeGuard = scopeGuard;
    }

    public async Task<TimetableCatalogDto> GetCatalogAsync(
        int? schoolId,
        CancellationToken cancellationToken = default)
    {
        var resolvedSchoolId = ResolveSchoolId(schoolId);
        var school = await _repository.GetSchools()
            .Where(x => x.Id == resolvedSchoolId && x.IsActive)
            .Select(x => new { x.Id, x.Name })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("المدرسة غير موجودة أو غير نشطة.");

        var capabilities = await GetCapabilitiesAsync(resolvedSchoolId, cancellationToken);
        var academicYears = await _repository.GetAcademicYears()
            .Select(x => new TimetableAcademicYearDto(x.Id, x.Code, x.NameAr, x.IsActive))
            .ToListAsync(cancellationToken);

        var teacherQuery = _repository.GetTeachers(resolvedSchoolId);
        if (ShouldLimitToCurrentInstructor(capabilities))
        {
            var currentUserId = RequireUserId();
            teacherQuery = teacherQuery.Where(x => x.UserId == currentUserId);
        }

        var teacherRows = await teacherQuery
            .Select(x => new
            {
                x.Id,
                x.UserId,
                FullName = x.User.FirstName + " " + x.User.LastName,
                x.EmployeeNumber,
                x.SubjectSpecialization,
                Classes = x.Classes.OrderBy(c => c.SortOrder).Select(c => c.ClassLabel).ToList()
            })
            .ToListAsync(cancellationToken);
        var teachers = teacherRows.Select(x => new TimetableTeacherDto(
            x.Id,
            x.UserId,
            x.FullName.Trim(),
            x.EmployeeNumber,
            x.SubjectSpecialization,
            x.Classes,
            x.UserId == _currentUser.UserId)).ToList();

        var moderators = capabilities.CanDelegate
            ? await BuildModeratorsAsync(resolvedSchoolId, cancellationToken)
            : new List<TimetableModeratorDto>();

        return new TimetableCatalogDto(
            school.Id,
            school.Name,
            academicYears,
            Enum.GetValues<TimetableSemester>().Select(x => new TimetableOptionDto((int)x, SemesterLabel(x))).ToList(),
            Enum.GetValues<TimetableDay>().Select(x => new TimetableOptionDto((int)x, DayLabel(x))).ToList(),
            PeriodCount,
            teachers,
            moderators,
            capabilities);
    }

    public async Task<SchoolTimetableDto?> GetCurrentAsync(
        int academicYearId,
        TimetableSemester semester,
        int? schoolId,
        CancellationToken cancellationToken = default)
    {
        EnsureSemester(semester);
        var resolvedSchoolId = ResolveSchoolId(schoolId);
        var capabilities = await GetCapabilitiesAsync(resolvedSchoolId, cancellationToken);
        var timetableId = await _repository.GetAll()
            .Where(x => x.SchoolId == resolvedSchoolId
                && x.AcademicYearId == academicYearId
                && x.Semester == semester
                && (x.IsPublished || capabilities.CanManage))
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return timetableId.HasValue
            ? await LoadDtoAsync(timetableId.Value, capabilities, cancellationToken)
            : null;
    }

    public async Task<SchoolTimetableDto> GetByIdAsync(
        int timetableId,
        CancellationToken cancellationToken = default)
    {
        var header = await _repository.GetAll()
            .Where(x => x.Id == timetableId)
            .Select(x => new { x.SchoolId, x.IsPublished })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("الجدول غير موجود.");
        EnsureSchoolScope(header.SchoolId);
        var capabilities = await GetCapabilitiesAsync(header.SchoolId, cancellationToken);
        if (!header.IsPublished && !capabilities.CanManage)
            throw new KeyNotFoundException("لا يوجد جدول منشور حاليًا.");
        return await LoadDtoAsync(timetableId, capabilities, cancellationToken);
    }

    public async Task<SchoolTimetableDto> CreateAsync(
        CreateSchoolTimetableRequest request,
        int? schoolId,
        CancellationToken cancellationToken = default)
    {
        EnsureSemester(request.Semester);
        var resolvedSchoolId = ResolveSchoolId(schoolId);
        await EnsureManageAsync(resolvedSchoolId, cancellationToken);
        if (!await _repository.AcademicYearExistsAsync(request.AcademicYearId, cancellationToken))
            throw new ArgumentException("العام الدراسي المحدد غير موجود.");
        var title = NormalizeRequired(request.Title, 250, "عنوان الجدول");
        if (await _repository.GetAll().AnyAsync(x =>
                x.SchoolId == resolvedSchoolId
                && x.AcademicYearId == request.AcademicYearId
                && x.Semester == request.Semester,
                cancellationToken))
            throw new InvalidOperationException("يوجد جدول بالفعل لهذا العام والفصل الدراسي.");

        var userId = RequireUserId();
        var now = DateTimeOffset.UtcNow;
        var timetable = new SchoolTimetable
        {
            SchoolId = resolvedSchoolId,
            AcademicYearId = request.AcademicYearId,
            Semester = request.Semester,
            Title = title,
            CreatedByUserId = userId,
            UpdatedByUserId = userId,
            CreatedAt = now,
            UpdatedAt = now
        };

        await _repository.ExecuteInTransactionAsync(async ct =>
        {
            await _repository.AddAsync(timetable, ct);
            await _repository.SaveChangesAsync(ct);
            await AddVersionAsync(timetable, Array.Empty<TimetableEntryDto>(), TimetableChangeKind.Created, null, ct);
            await _repository.SaveChangesAsync(ct);
        }, cancellationToken);

        return await GetByIdAsync(timetable.Id, cancellationToken);
    }

    public async Task<SchoolTimetableDto> SaveAsync(
        int timetableId,
        SaveSchoolTimetableRequest request,
        CancellationToken cancellationToken = default)
    {
        var timetable = await RequireTrackedAsync(timetableId, cancellationToken);
        await EnsureManageAsync(timetable.SchoolId, cancellationToken);
        EnsureRevision(timetable, request.Revision);
        var title = NormalizeRequired(request.Title, 250, "عنوان الجدول");
        var entries = await NormalizeAndValidateEntriesAsync(timetable.SchoolId, request.Entries, cancellationToken);
        await ReplaceAsync(timetable, title, entries, TimetableChangeKind.Saved, null, cancellationToken);
        return await GetByIdAsync(timetableId, cancellationToken);
    }

    public async Task<SchoolTimetableDto> PublishAsync(
        int timetableId,
        TimetableRevisionRequest request,
        CancellationToken cancellationToken = default)
    {
        var timetable = await RequireTrackedAsync(timetableId, cancellationToken);
        await EnsureManageAsync(timetable.SchoolId, cancellationToken);
        EnsureRevision(timetable, request.Revision);
        if (!timetable.Entries.Any(x => !x.IsDeleted))
            throw new InvalidOperationException("لا يمكن نشر جدول فارغ.");
        if (timetable.IsPublished)
            return await GetByIdAsync(timetableId, cancellationToken);

        var userId = RequireUserId();
        var now = DateTimeOffset.UtcNow;
        timetable.IsPublished = true;
        timetable.PublishedAt = now;
        timetable.PublishedByUserId = userId;
        timetable.UpdatedByUserId = userId;
        timetable.UpdatedAt = now;
        timetable.Revision++;

        var snapshotEntries = timetable.Entries.Where(x => !x.IsDeleted).Select(ToDto).ToList();
        await _repository.ExecuteInTransactionAsync(async ct =>
        {
            await SaveWithConcurrencyMessageAsync(ct);
            await AddVersionAsync(timetable, snapshotEntries, TimetableChangeKind.Published, null, ct);
            await _repository.SaveChangesAsync(ct);
        }, cancellationToken);

        return await GetByIdAsync(timetableId, cancellationToken);
    }

    public async Task<IReadOnlyList<TimetableVersionDto>> GetVersionsAsync(
        int timetableId,
        CancellationToken cancellationToken = default)
    {
        var timetable = await RequireHeaderAsync(timetableId, cancellationToken);
        await EnsureManageAsync(timetable.SchoolId, cancellationToken);
        return await _repository.GetVersions(timetableId)
            .OrderByDescending(x => x.VersionNumber)
            .Select(x => new TimetableVersionDto(
                x.Id,
                x.VersionNumber,
                x.ChangeKind,
                ChangeKindLabel(x.ChangeKind),
                x.Title,
                x.CreatedAt,
                (x.CreatedByUser.FirstName + " " + x.CreatedByUser.LastName).Trim(),
                x.RestoredFromVersionNumber))
            .ToListAsync(cancellationToken);
    }

    public async Task<SchoolTimetableDto> RestoreAsync(
        int timetableId,
        int versionNumber,
        TimetableRevisionRequest request,
        CancellationToken cancellationToken = default)
    {
        var timetable = await RequireTrackedAsync(timetableId, cancellationToken);
        await EnsureManageAsync(timetable.SchoolId, cancellationToken);
        EnsureRevision(timetable, request.Revision);
        var version = await _repository.GetVersions(timetableId)
            .FirstOrDefaultAsync(x => x.VersionNumber == versionNumber, cancellationToken)
            ?? throw new KeyNotFoundException("نسخة الجدول المطلوبة غير موجودة.");
        var snapshot = JsonSerializer.Deserialize<TimetableSnapshotDto>(version.SnapshotJson)
            ?? throw new InvalidOperationException("تعذر قراءة نسخة الجدول المحفوظة.");
        var requests = snapshot.Entries.Select(x => new SaveTimetableEntryRequest(
            x.InstructorProfileId, x.Day, x.Period, x.EntryType, x.ClassLabel, x.Subject)).ToList();
        var entries = await NormalizeAndValidateEntriesAsync(timetable.SchoolId, requests, cancellationToken);
        await ReplaceAsync(timetable, snapshot.Title, entries, TimetableChangeKind.Restored, versionNumber, cancellationToken);
        return await GetByIdAsync(timetableId, cancellationToken);
    }

    public async Task<IReadOnlyList<TimetableModeratorDto>> UpdateGrantsAsync(
        UpdateTimetableGrantsRequest request,
        int? schoolId,
        CancellationToken cancellationToken = default)
    {
        var resolvedSchoolId = ResolveSchoolId(schoolId);
        await EnsureDelegateAsync(resolvedSchoolId, cancellationToken);
        var selectedIds = request.ModeratorUserIds
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToHashSet(StringComparer.Ordinal);
        var validIds = await _repository.GetModerators(resolvedSchoolId)
            .Where(x => selectedIds.Contains(x.Id))
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);
        if (validIds.Count != selectedIds.Count)
            throw new ArgumentException("تحتوي قائمة التفويض على مستخدم ليس مشرفًا نشطًا في هذه المدرسة.");

        var userId = RequireUserId();
        var now = DateTimeOffset.UtcNow;
        await _repository.ExecuteInTransactionAsync(async ct =>
        {
            var existing = await _repository.GetTrackedGrantsAsync(resolvedSchoolId, ct);
            _repository.SoftDeleteGrants(existing, userId, now);
            if (existing.Count > 0) await _repository.SaveChangesAsync(ct);
            await _repository.AddGrantsAsync(selectedIds.Select(id => new TimetableEditorGrant
            {
                SchoolId = resolvedSchoolId,
                ModeratorUserId = id,
                GrantedByUserId = userId,
                GrantedAt = now
            }), ct);
            await _repository.SaveChangesAsync(ct);
        }, cancellationToken);

        return await BuildModeratorsAsync(resolvedSchoolId, cancellationToken);
    }

    public async Task<TimetableImportResultDto> ImportAsync(
        int timetableId,
        Stream stream,
        int revision,
        CancellationToken cancellationToken = default)
    {
        var timetable = await RequireTrackedAsync(timetableId, cancellationToken);
        await EnsureManageAsync(timetable.SchoolId, cancellationToken);
        EnsureRevision(timetable, revision);
        var catalog = await GetCatalogAsync(timetable.SchoolId, cancellationToken);
        var imported = _documents.ParseImport(stream, catalog);
        var requests = imported.Rows.SelectMany(x => x.Entries).ToList();
        var entries = await NormalizeAndValidateEntriesAsync(timetable.SchoolId, requests, cancellationToken);
        await ReplaceAsync(timetable, timetable.Title, entries, TimetableChangeKind.Imported, null, cancellationToken);
        var dto = await GetByIdAsync(timetableId, cancellationToken);
        return new TimetableImportResultDto(dto, entries.Count, imported.Warnings);
    }

    public async Task<TimetableFileDto> BuildImportTemplateAsync(
        int timetableId,
        CancellationToken cancellationToken = default)
    {
        var timetable = await GetByIdAsync(timetableId, cancellationToken);
        if (!timetable.Capabilities.CanManage)
            throw new UnauthorizedSchoolAccessException("ليس لديك صلاحية تنزيل نموذج تعديل الجدول.");
        var catalog = await GetCatalogAsync(timetable.SchoolId, cancellationToken);
        return _documents.BuildImportTemplate(timetable, catalog);
    }

    public async Task<TimetableFileDto> BuildPdfAsync(
        int timetableId,
        TimetablePdfColorMode colorMode,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(colorMode))
            throw new ArgumentException("نمط ألوان ملف PDF غير صالح.", nameof(colorMode));
        var timetable = await GetByIdAsync(timetableId, cancellationToken);
        var catalog = await GetCatalogAsync(timetable.SchoolId, cancellationToken);
        return _documents.BuildPdf(timetable, catalog, colorMode);
    }

    private async Task ReplaceAsync(
        SchoolTimetable timetable,
        string title,
        IReadOnlyList<TimetableEntryDto> entries,
        TimetableChangeKind changeKind,
        int? restoredFromVersion,
        CancellationToken cancellationToken)
    {
        var userId = RequireUserId();
        var now = DateTimeOffset.UtcNow;
        await _repository.ExecuteInTransactionAsync(async ct =>
        {
            var currentEntries = timetable.Entries.Where(x => !x.IsDeleted).ToList();
            _repository.SoftDeleteEntries(currentEntries, now);
            if (currentEntries.Count > 0) await _repository.SaveChangesAsync(ct);

            var newEntries = entries.Select(x => new SchoolTimetableEntry
            {
                SchoolId = timetable.SchoolId,
                SchoolTimetableId = timetable.Id,
                InstructorProfileId = x.InstructorProfileId,
                Day = x.Day,
                Period = x.Period,
                EntryType = x.EntryType,
                ClassLabel = x.ClassLabel,
                Subject = x.Subject,
                CreatedAt = now,
                UpdatedAt = now
            }).ToList();
            await _repository.AddEntriesAsync(newEntries, ct);
            timetable.Title = title;
            timetable.UpdatedAt = now;
            timetable.UpdatedByUserId = userId;
            timetable.Revision++;
            await SaveWithConcurrencyMessageAsync(ct);
            await AddVersionAsync(timetable, entries, changeKind, restoredFromVersion, ct);
            await _repository.SaveChangesAsync(ct);
        }, cancellationToken);
    }

    private async Task AddVersionAsync(
        SchoolTimetable timetable,
        IReadOnlyList<TimetableEntryDto> entries,
        TimetableChangeKind changeKind,
        int? restoredFromVersion,
        CancellationToken cancellationToken)
    {
        var lastVersion = await _repository.GetVersions(timetable.Id)
            .Select(x => (int?)x.VersionNumber)
            .MaxAsync(cancellationToken) ?? 0;
        var snapshot = JsonSerializer.Serialize(new TimetableSnapshotDto(timetable.Title, entries));
        await _repository.AddVersionAsync(new SchoolTimetableVersion
        {
            SchoolTimetableId = timetable.Id,
            VersionNumber = lastVersion + 1,
            ChangeKind = changeKind,
            Title = timetable.Title,
            SnapshotJson = snapshot,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedByUserId = RequireUserId(),
            RestoredFromVersionNumber = restoredFromVersion
        }, cancellationToken);
    }

    private async Task<List<TimetableEntryDto>> NormalizeAndValidateEntriesAsync(
        int schoolId,
        IReadOnlyList<SaveTimetableEntryRequest> requests,
        CancellationToken cancellationToken)
    {
        var teacherIds = await _repository.GetTeachers(schoolId).Select(x => x.Id).ToListAsync(cancellationToken);
        var allowedTeachers = teacherIds.ToHashSet();
        var entries = new List<TimetableEntryDto>(requests.Count);
        foreach (var item in requests)
        {
            if (!allowedTeachers.Contains(item.InstructorProfileId))
                throw new ArgumentException("إحدى الخانات مرتبطة بمعلم غير نشط أو خارج المدرسة.");
            if (!Enum.IsDefined(item.Day)) throw new ArgumentException("يوم دراسي غير صالح.");
            if (item.Period is < 1 or > PeriodCount) throw new ArgumentException("رقم الفترة يجب أن يكون من 1 إلى 8.");
            if (!Enum.IsDefined(item.EntryType)) throw new ArgumentException("نوع خانة الجدول غير صالح.");

            if (item.EntryType == TimetableEntryType.Standby)
            {
                entries.Add(new TimetableEntryDto(item.InstructorProfileId, item.Day, item.Period, item.EntryType, null, null));
                continue;
            }

            var classLabel = NormalizeRequired(item.ClassLabel, 50, "الفصل");
            var subject = NormalizeRequired(item.Subject, 200, "المادة");
            entries.Add(new TimetableEntryDto(item.InstructorProfileId, item.Day, item.Period, item.EntryType, classLabel, subject));
        }

        var duplicateTeacherSlot = entries.GroupBy(x => new { x.InstructorProfileId, x.Day, x.Period }).FirstOrDefault(x => x.Count() > 1);
        if (duplicateTeacherSlot != null)
            throw new ArgumentException("لا يمكن وضع أكثر من خانة للمعلم في اليوم والفترة نفسيهما.");

        var classConflict = entries
            .Where(x => x.EntryType == TimetableEntryType.Lesson)
            .GroupBy(x => new { x.Day, x.Period, ClassLabel = x.ClassLabel!.Trim().ToUpperInvariant() })
            .FirstOrDefault(x => x.Select(e => e.InstructorProfileId).Distinct().Count() > 1);
        if (classConflict != null)
            throw new ArgumentException($"الفصل «{classConflict.First().ClassLabel}» مسند لأكثر من معلم في {DayLabel(classConflict.Key.Day)}، الفترة {classConflict.Key.Period}.");

        return entries;
    }

    private async Task<SchoolTimetableDto> LoadDtoAsync(
        int timetableId,
        TimetableCapabilitiesDto capabilities,
        CancellationToken cancellationToken)
    {
        var currentInstructorUserId = ShouldLimitToCurrentInstructor(capabilities)
            ? RequireUserId()
            : null;
        var data = await _repository.GetAll()
            .Where(x => x.Id == timetableId)
            .Select(x => new TimetableData(
                x.Id,
                x.SchoolId,
                x.AcademicYearId,
                x.AcademicYear.NameAr,
                x.Semester,
                x.Title,
                x.IsPublished,
                x.PublishedAt,
                x.Revision,
                x.UpdatedAt,
                x.Entries
                    .Where(e => currentInstructorUserId == null || e.InstructorProfile.UserId == currentInstructorUserId)
                    .OrderBy(e => e.InstructorProfileId).ThenBy(e => e.Day).ThenBy(e => e.Period)
                    .Select(e => new TimetableEntryDto(e.InstructorProfileId, e.Day, e.Period, e.EntryType, e.ClassLabel, e.Subject))
                    .ToList()))
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("الجدول غير موجود.");
        var summaries = data.Entries
            .GroupBy(x => x.InstructorProfileId)
            .Select(x => new TimetableTeacherSummaryDto(
                x.Key,
                x.Count(e => e.EntryType == TimetableEntryType.Lesson),
                x.Count(e => e.EntryType == TimetableEntryType.Standby)))
            .ToList();
        return new SchoolTimetableDto(
            data.Id,
            data.SchoolId,
            data.AcademicYearId,
            data.AcademicYearName,
            data.Semester,
            SemesterLabel(data.Semester),
            data.Title,
            data.IsPublished,
            data.PublishedAt,
            data.Revision,
            data.UpdatedAt,
            data.Entries,
            summaries,
            capabilities);
    }

    private bool ShouldLimitToCurrentInstructor(TimetableCapabilitiesDto capabilities) =>
        !capabilities.CanManage && _currentUser.IsInRole(RoleNames.Instructor);

    private async Task<TimetableCapabilitiesDto> GetCapabilitiesAsync(int schoolId, CancellationToken cancellationToken)
    {
        var canDelegate = _currentUser.IsGlobalAdmin() || _currentUser.IsInRole(RoleNames.SchoolManager);
        var canManage = canDelegate;
        if (!canManage && _currentUser.IsInRole(RoleNames.Moderator))
        {
            var userId = RequireUserId();
            canManage = await _repository.GetGrants(schoolId).AnyAsync(x => x.ModeratorUserId == userId, cancellationToken);
        }
        return new TimetableCapabilitiesDto(canManage, canDelegate, canManage);
    }

    private async Task EnsureManageAsync(int schoolId, CancellationToken cancellationToken)
    {
        EnsureSchoolScope(schoolId);
        if (!(await GetCapabilitiesAsync(schoolId, cancellationToken)).CanManage)
            throw new UnauthorizedSchoolAccessException("إدارة الجدول متاحة لمدير المدرسة أو المشرف المفوّض فقط.");
    }

    private Task EnsureDelegateAsync(int schoolId, CancellationToken cancellationToken)
    {
        EnsureSchoolScope(schoolId);
        if (!_currentUser.IsGlobalAdmin() && !_currentUser.IsInRole(RoleNames.SchoolManager))
            throw new UnauthorizedSchoolAccessException("تفويض المشرفين متاح لمدير المدرسة فقط.");
        return Task.CompletedTask;
    }

    private async Task<List<TimetableModeratorDto>> BuildModeratorsAsync(int schoolId, CancellationToken cancellationToken)
    {
        var grantedIds = await _repository.GetGrants(schoolId).Select(x => x.ModeratorUserId).ToListAsync(cancellationToken);
        var granted = grantedIds.ToHashSet(StringComparer.Ordinal);
        var rows = await _repository.GetModerators(schoolId)
            .Select(x => new { x.Id, FullName = x.FirstName + " " + x.LastName })
            .ToListAsync(cancellationToken);
        return rows.Select(x => new TimetableModeratorDto(x.Id, x.FullName.Trim(), granted.Contains(x.Id))).ToList();
    }

    private async Task<SchoolTimetable> RequireTrackedAsync(int timetableId, CancellationToken cancellationToken)
    {
        var timetable = await _repository.GetTrackedWithEntriesAsync(timetableId, cancellationToken)
            ?? throw new KeyNotFoundException("الجدول غير موجود.");
        EnsureSchoolScope(timetable.SchoolId);
        return timetable;
    }

    private async Task<SchoolTimetable> RequireHeaderAsync(int timetableId, CancellationToken cancellationToken)
    {
        var timetable = await _repository.GetAll().FirstOrDefaultAsync(x => x.Id == timetableId, cancellationToken)
            ?? throw new KeyNotFoundException("الجدول غير موجود.");
        EnsureSchoolScope(timetable.SchoolId);
        return timetable;
    }

    private int ResolveSchoolId(int? requestedSchoolId)
    {
        var schoolId = _scopeGuard.ResolveAllowedSchoolId(requestedSchoolId);
        return schoolId ?? throw new ArgumentException("يجب تحديد المدرسة.");
    }

    private void EnsureSchoolScope(int schoolId)
    {
        var allowed = _scopeGuard.ResolveAllowedSchoolId(schoolId);
        if (allowed != schoolId)
            throw UnauthorizedSchoolAccessException.OutsideScope(allowed, schoolId);
    }

    private string RequireUserId() => _currentUser.UserId
        ?? throw new UnauthorizedAccessException("يجب تسجيل الدخول أولًا.");

    private async Task SaveWithConcurrencyMessageAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _repository.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new InvalidOperationException("تم تعديل الجدول بواسطة مستخدم آخر. حدّث الصفحة ثم أعد المحاولة.");
        }
    }

    private static void EnsureRevision(SchoolTimetable timetable, int revision)
    {
        if (revision != timetable.Revision)
            throw new InvalidOperationException("هذه النسخة من الجدول قديمة. حدّث الصفحة قبل الحفظ.");
    }

    private static void EnsureSemester(TimetableSemester semester)
    {
        if (!Enum.IsDefined(semester)) throw new ArgumentException("الفصل الدراسي غير صالح.");
    }

    private static string NormalizeRequired(string? value, int maxLength, string field)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized)) throw new ArgumentException($"{field} مطلوب.");
        if (normalized.Length > maxLength) throw new ArgumentException($"{field} لا يمكن أن يتجاوز {maxLength} حرفًا.");
        return normalized;
    }

    private static TimetableEntryDto ToDto(SchoolTimetableEntry entry) => new(
        entry.InstructorProfileId,
        entry.Day,
        entry.Period,
        entry.EntryType,
        entry.ClassLabel,
        entry.Subject);

    private static string SemesterLabel(TimetableSemester semester) => semester switch
    {
        TimetableSemester.First => "الفصل الدراسي الأول",
        TimetableSemester.Second => "الفصل الدراسي الثاني",
        _ => semester.ToString()
    };

    internal static string DayLabel(TimetableDay day) => day switch
    {
        TimetableDay.Saturday => "السبت",
        TimetableDay.Sunday => "الأحد",
        TimetableDay.Monday => "الاثنين",
        TimetableDay.Tuesday => "الثلاثاء",
        TimetableDay.Wednesday => "الأربعاء",
        TimetableDay.Thursday => "الخميس",
        _ => day.ToString()
    };

    private static string ChangeKindLabel(TimetableChangeKind kind) => kind switch
    {
        TimetableChangeKind.Created => "إنشاء",
        TimetableChangeKind.Saved => "حفظ",
        TimetableChangeKind.Published => "نشر",
        TimetableChangeKind.Imported => "استيراد Excel",
        TimetableChangeKind.Restored => "استرجاع نسخة",
        _ => kind.ToString()
    };

    private sealed record TimetableData(
        int Id,
        int SchoolId,
        int AcademicYearId,
        string AcademicYearName,
        TimetableSemester Semester,
        string Title,
        bool IsPublished,
        DateTimeOffset? PublishedAt,
        int Revision,
        DateTimeOffset UpdatedAt,
        List<TimetableEntryDto> Entries);
}
