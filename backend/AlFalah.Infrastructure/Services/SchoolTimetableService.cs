using AlFalah.Application.IntelligentTimetable;
using AlFalah.Application.IntelligentTimetable.DTOs;
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
    private readonly TimetableReviewService _review;
    private readonly IBellScheduleRepository _timings;
    private readonly TeacherAvailabilityService _availability;
    private readonly SubjectAssignmentService _subjects;
    private readonly TimetableGenerationService _generation;
    private readonly ISchoolTimetableRepository _repository;
    private readonly ISchoolTimetableDocumentService _documents;
    private readonly ICurrentUserService _currentUser;
    private readonly SchoolScopeGuard _scopeGuard;
    private readonly IWeeklyStandbyRepository _standbyRepository;
    private readonly WeeklyStandbyEngine _standby;

    public SchoolTimetableService(
        ISchoolTimetableRepository repository,
        ISchoolTimetableDocumentService documents,
        ICurrentUserService currentUser,
        SchoolScopeGuard scopeGuard, IBellScheduleRepository timings, TeacherAvailabilityService availability, SubjectAssignmentService subjects,
        TimetableReviewService review, TimetableGenerationService generation, IWeeklyStandbyRepository standbyRepository, WeeklyStandbyEngine standby)
    {
        _repository = repository;
        _documents = documents;
        _currentUser = currentUser;
        _scopeGuard = scopeGuard;
        _timings = timings;
        _availability = availability;
        _subjects = subjects;
        _review = review;
        _generation = generation;
        _standbyRepository = standbyRepository;
        _standby = standby;
    }

    public async Task<IReadOnlyList<TimetableListItemDto>> ListAsync(int academicYearId, TimetableSemester semester,
        int? schoolId, CancellationToken cancellationToken = default)
    {
        EnsureSemester(semester);
        var resolvedSchoolId = ResolveSchoolId(schoolId);
        await EnsureManageAsync(resolvedSchoolId, cancellationToken);
        return await _repository.GetAll()
            .Where(x => x.SchoolId == resolvedSchoolId && x.AcademicYearId == academicYearId && x.Semester == semester)
            .OrderBy(x => x.IsClone).ThenByDescending(x => x.UpdatedAt).ThenByDescending(x => x.Id)
            .Select(x => new TimetableListItemDto(x.Id, x.Title, x.IsClone, x.IsPublished))
            .ToListAsync(cancellationToken);
    }

    public async Task<TimetableCatalogDto> GetCatalogAsync(
        int? schoolId,
        CancellationToken cancellationToken = default,
        bool personalOnly = true,
        int? timetableSetupProfileId = null)
    {
        var resolvedSchoolId = ResolveSchoolId(schoolId);
        var school = await _repository.GetSchools()
            .Where(x => x.Id == resolvedSchoolId && x.IsActive)
            .Select(x => new { x.Id, x.Name })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("المدرسة غير موجودة أو غير نشطة.");

        var capabilities = await GetCapabilitiesAsync(resolvedSchoolId, cancellationToken);
        var academicYears = await _repository.GetAcademicTerms(resolvedSchoolId)
            .GroupBy(term => new
            {
                term.AcademicYear.Id,
                term.AcademicYear.Code,
                term.AcademicYear.NameAr,
                term.AcademicYear.StartsOn
            })
            .OrderByDescending(group => group.Key.StartsOn)
            .Select(group => new TimetableAcademicYearDto(
                group.Key.Id,
                group.Key.Code,
                group.Key.NameAr,
                group.Any(term => term.IsActive)))
            .ToListAsync(cancellationToken);

        var teacherQuery = _repository.GetTeachers(resolvedSchoolId);
        if (ShouldLimitToCurrentInstructor(capabilities, personalOnly))
        {
            var currentUserId = RequireUserId();
            teacherQuery = teacherQuery.Where(x => x.UserId == currentUserId);
        }

        var classYearIds = academicYears.Where(y => y.IsActive).Select(y => y.Id).ToArray();
        var teacherRows = await teacherQuery
            .Select(x => new
            {
                x.Id,
                x.UserId,
                FullName = x.User.FirstName + " " + x.User.LastName,
                x.EmployeeNumber,
                x.SubjectSpecialization,
                Classes = x.Classes.Where(c => c.SchoolId == resolvedSchoolId && c.ClassroomId != null &&
                        c.Classroom != null && c.Classroom.IsActive && c.Classroom.AcademicYear.IsActive &&
                        classYearIds.Contains(c.Classroom.AcademicYearId))
                    .OrderBy(c => c.Classroom!.Stage).ThenBy(c => c.Classroom!.GradeLevel)
                    .ThenBy(c => c.Classroom!.SectionNumber).ThenBy(c => c.Classroom!.Section)
                    .Select(c => c.Classroom!.ClassLabel).ToList()
            })
            .ToListAsync(cancellationToken);
        var printProfiles = timetableSetupProfileId is int setupId
            ? await _repository.GetTeacherTimetableProfiles(resolvedSchoolId, setupId)
                .Select(x => new { x.InstructorProfileId, x.ShortDisplayName, x.HideFromPrint })
                .ToDictionaryAsync(x => x.InstructorProfileId, x => (x.ShortDisplayName, x.HideFromPrint), cancellationToken)
            : new Dictionary<int, (string ShortDisplayName, bool HideFromPrint)>();
        var teachers = teacherRows.Select(x =>
        {
            printProfiles.TryGetValue(x.Id, out var profile);
            return new TimetableTeacherDto(
                x.Id,
                x.UserId,
                x.FullName.Trim(),
                x.EmployeeNumber,
                x.SubjectSpecialization,
                x.Classes,
                x.UserId == _currentUser.UserId,
                profile.ShortDisplayName,
                profile.HideFromPrint);
        }).ToList();

        var moderators = capabilities.CanDelegate
            ? await BuildModeratorsAsync(resolvedSchoolId, cancellationToken)
            : new List<TimetableModeratorDto>();

        return new TimetableCatalogDto(
            school.Id,
            school.Name,
            academicYears,
            Enum.GetValues<TimetableSemester>().Select(x => new TimetableOptionDto((int)x, SemesterLabel(x))).ToList(),
            Enum.GetValues<TimetableDay>().Select(x => new TimetableOptionDto((int)x, DayLabel(x))).ToList(),
            0,
            teachers,
            moderators,
            capabilities);
    }

    public async Task<SchoolTimetableDto?> GetCurrentAsync(
        int academicYearId,
        TimetableSemester semester,
        int? schoolId,
        CancellationToken cancellationToken = default,
        bool personalOnly = true)
    {
        EnsureSemester(semester);
        var resolvedSchoolId = ResolveSchoolId(schoolId);
        var capabilities = await GetCapabilitiesAsync(resolvedSchoolId, cancellationToken);
        var timetableId = await _repository.GetAll()
            .Where(x => x.SchoolId == resolvedSchoolId
                && x.AcademicYearId == academicYearId
                && x.Semester == semester
                && !x.IsClone
                && (x.IsPublished || capabilities.CanManage))
            .OrderByDescending(x => !x.TimingsRequireRevalidation
                && x.SetupRevision.HasValue
                && x.TimetableSetupProfileId.HasValue
                && x.TimetableSetupProfile != null
                && x.SetupRevision == x.TimetableSetupProfile.Revision)
            .ThenByDescending(x => x.Entries.Any())
            .ThenByDescending(x => x.IsPublished)
            .ThenByDescending(x => x.UpdatedAt)
            .ThenByDescending(x => x.Id)
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return timetableId.HasValue
            ? await LoadDtoAsync(timetableId.Value, capabilities, cancellationToken, personalOnly)
            : null;
    }

    public async Task<SchoolTimetableDto> GetByIdAsync(
        int timetableId,
        CancellationToken cancellationToken = default,
        bool personalOnly = true)
    {
        var header = await _repository.GetAll()
            .Where(x => x.Id == timetableId)
            .Select(x => new { x.SchoolId, x.IsPublished, x.IsClone })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("الجدول غير موجود.");
        EnsureSchoolScope(header.SchoolId);
        var capabilities = await GetCapabilitiesAsync(header.SchoolId, cancellationToken);
        if ((!header.IsPublished || header.IsClone) && !capabilities.CanManage)
            throw new KeyNotFoundException("لا يوجد جدول منشور حاليًا.");
        return await LoadDtoAsync(timetableId, capabilities, cancellationToken, personalOnly);
    }

    public async Task<SchoolTimetableDto> CreateAsync(
        CreateSchoolTimetableRequest request,
        int? schoolId,
        CancellationToken cancellationToken = default)
    {
        EnsureSemester(request.Semester);
        var resolvedSchoolId = ResolveSchoolId(schoolId);
        await EnsureManageAsync(resolvedSchoolId, cancellationToken);
        if (!await _repository.AcademicScopeExistsAsync(
                resolvedSchoolId,
                request.AcademicYearId,
                request.Semester,
                cancellationToken))
            throw new ArgumentException("العام أو الفصل الدراسي المحدد غير متاح للمدرسة النشطة.");
        var title = NormalizeRequired(request.Title, 250, "عنوان الجدول");
        if (await _repository.GetAll().AnyAsync(x =>
                x.SchoolId == resolvedSchoolId
                && x.AcademicYearId == request.AcademicYearId
                && x.Semester == request.Semester,
                cancellationToken))
            throw new InvalidOperationException("يوجد جدول بالفعل لهذا العام والفصل الدراسي.");

        var timing = await _timings.GetSelectedAsync(resolvedSchoolId, request.AcademicYearId, request.Semester, request.TimetableSetupProfileId, cancellationToken)
            ?? throw new ArgumentException("اختر قالب توقيت صالحاً في إعدادات الجدول أولاً.");
        var userId = RequireUserId();
        var now = DateTimeOffset.UtcNow;
        var timetable = new SchoolTimetable
        {
            SchoolId = resolvedSchoolId,
            AcademicYearId = request.AcademicYearId,
            Semester = request.Semester,
            Title = title,
            TimetableSetupProfileId = request.TimetableSetupProfileId ?? timing.SelectedByProfileIds.FirstOrDefault(),
            BellScheduleRevisionId = timing.RevisionId,
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

    public async Task<SchoolTimetableDto> RegenerateAsync(
        int timetableId,
        TimetableRevisionRequest request,
        CancellationToken cancellationToken = default)
    {
        var timetable = await RequireTrackedAsync(timetableId, cancellationToken);
        await EnsureManageAsync(timetable.SchoolId, cancellationToken);
        EnsureRevision(timetable, request.Revision);
        if (timetable.TimetableSetupProfileId is null || timetable.BellScheduleRevisionId is null)
            throw new ArgumentException("اربط الجدول بملف إعداد وتوقيت صالح قبل إعادة التوليد.");

        var generated = (await _generation.GenerateAsync(timetableId, request.Revision, cancellationToken))
            .Concat(SnapshotEntries(timetable.Entries).Where(x => x.EntryType == TimetableEntryType.Standby)).ToArray();
        await _availability.ValidateAssignmentsAsync(timetable, generated, cancellationToken);
        var timing = await _timings.GetRevisionAsync(timetable.SchoolId, timetable.BellScheduleRevisionId.Value, cancellationToken)
            ?? throw new ArgumentException("توقيت الجدول الحالي غير صالح.");
        var validated = await _subjects.ValidateGeneratedAsync(timetable, generated, timing, cancellationToken);
        await ReplaceAsync(timetable, timetable.Title, validated.Entries, TimetableChangeKind.Regenerated, null,
            cancellationToken, allowPublished: true, setupRevision: validated.SetupRevision);
        return await GetByIdAsync(timetableId, cancellationToken);
    }

    public async Task<SchoolTimetableDto> SaveAsync(
        int timetableId,
        SaveSchoolTimetableRequest request,
        CancellationToken cancellationToken = default)
    {
        var timetable = await RequireTrackedAsync(timetableId, cancellationToken);
        await EnsureManageAsync(timetable.SchoolId, cancellationToken);
        EnsureDraft(timetable);
        EnsureRevision(timetable, request.Revision);
        var title = NormalizeRequired(request.Title, 250, "عنوان الجدول");
        var entries = await NormalizeAndValidateEntriesAsync(timetable, request.Entries, cancellationToken);
        await ReplaceAsync(timetable, title, entries, TimetableChangeKind.Saved, null, cancellationToken);
        return await GetByIdAsync(timetableId, cancellationToken);
    }

    public async Task<SchoolTimetableDto> PublishAsync(
        int timetableId,
        TimetableRevisionRequest request,
        CancellationToken cancellationToken = default)
    {
        try { await _review.PublishAsync(timetableId, request.Revision, cancellationToken); }
        catch (BellScheduleConflictException ex) { throw new InvalidOperationException("هذه النسخة قديمة؛ أعد مراجعة الجدول.", ex); }
        return await GetByIdAsync(timetableId, cancellationToken);
    }

    public async Task<IReadOnlyList<TimetableVersionDto>> GetVersionsAsync(
        int timetableId,
        CancellationToken cancellationToken = default)
    {
        var timetable = await RequireHeaderAsync(timetableId, cancellationToken);
        await EnsureManageAsync(timetable.SchoolId, cancellationToken);
        var rows = await _repository.GetVersions(timetableId)
            .Include(x => x.CreatedByUser)
            .OrderByDescending(x => x.VersionNumber)
            .ToListAsync(cancellationToken);
        return rows.Select(x => new TimetableVersionDto(
                x.Id,
                x.VersionNumber,
                x.ChangeKind,
                ChangeKindLabel(x.ChangeKind),
                x.Title,
                x.CreatedAt,
                (x.CreatedByUser.FirstName + " " + x.CreatedByUser.LastName).Trim(),
                x.RestoredFromVersionNumber,
                x.CheckpointName,
                x.CheckpointNote,
                x.SourceTimetableId,
                x.SourceVersionNumber,
                x.ChangeSummary,
                DeserializeChangeDetails(x.ChangeDetailsJson)))
            .ToList();
    }

    public async Task<SchoolTimetableDto> CreateCheckpointAsync(
        int timetableId,
        CreateTimetableCheckpointRequest request,
        CancellationToken cancellationToken = default)
    {
        var timetable = await RequireTrackedAsync(timetableId, cancellationToken);
        await EnsureManageAsync(timetable.SchoolId, cancellationToken);
        EnsureRevision(timetable, request.Revision);
        var name = NormalizeRequired(request.Name, 150, "اسم نقطة الاستعادة");
        var note = NormalizeOptional(request.Note, 1000);
        await _repository.ExecuteInTransactionAsync(async ct =>
        {
            var current = await _repository.GetAll().Include(x => x.Entries).SingleAsync(x => x.Id == timetableId, ct);
            EnsureRevision(current, request.Revision);
            await AddVersionAsync(current, SnapshotEntries(current.Entries), TimetableChangeKind.Checkpoint, null, ct,
                checkpointName: name, checkpointNote: note,
                audit: new ChangeAudit("نقطة استعادة محفوظة", [new TimetableChangeDetailDto("checkpoint", "لقطة كاملة محفوظة", 1, null, name)]));
            await _repository.SaveChangesAsync(ct);
        }, cancellationToken);

        return await GetByIdAsync(timetableId, cancellationToken);
    }

    public async Task<SchoolTimetableDto> CloneAsync(
        int timetableId,
        CloneTimetableRequest request,
        CancellationToken cancellationToken = default)
    {
        var header = await RequireHeaderAsync(timetableId, cancellationToken);
        await EnsureManageAsync(header.SchoolId, cancellationToken);
        var title = NormalizeRequired(request.Title, 250, "عنوان النسخة");
        var cloneId = 0;
        await _repository.ExecuteInTransactionAsync(async ct =>
        {
            var source = await _repository.GetAll().Include(x => x.Entries).SingleAsync(x => x.Id == timetableId, ct);
            EnsureRevision(source, request.Revision);
            SchoolTimetableVersion? sourceVersion = null;
            TimetableSnapshotDto snapshot;
            if (request.VersionNumber.HasValue)
            {
                sourceVersion = await _repository.GetVersions(source.Id)
                    .FirstOrDefaultAsync(x => x.VersionNumber == request.VersionNumber.Value, cancellationToken)
                    ?? throw new KeyNotFoundException("نسخة المصدر المطلوبة غير موجودة.");
                snapshot = JsonSerializer.Deserialize<TimetableSnapshotDto>(sourceVersion.SnapshotJson)
                    ?? throw new InvalidOperationException("تعذر قراءة نسخة المصدر.");
            }
            else
            {
                var timing = source.BellScheduleRevisionId.HasValue
                    ? await _timings.GetRevisionAsync(source.SchoolId, source.BellScheduleRevisionId.Value, cancellationToken)
                    : null;
                snapshot = new TimetableSnapshotDto(source.Title, SnapshotEntries(source.Entries), timing,
                    source.TimetableSetupProfileId, source.SetupRevision, source.StandbySettingsJson);
            }

            var bellRevisionId = snapshot.BellSchedule?.RevisionId ?? source.BellScheduleRevisionId;
            if (!bellRevisionId.HasValue)
                throw new ArgumentException("الجدول المصدر لا يحتوي على توقيت صالح.");
            var userId = RequireUserId();
            var now = DateTimeOffset.UtcNow;
            var clone = new SchoolTimetable
            {
                SchoolId = source.SchoolId,
                AcademicYearId = source.AcademicYearId,
                Semester = source.Semester,
                Title = title,
                StandbySettingsJson = snapshot.StandbySettingsJson ?? source.StandbySettingsJson,
                BellScheduleRevisionId = bellRevisionId,
                TimetableSetupProfileId = source.TimetableSetupProfileId,
                SetupRevision = source.SetupRevision,
                TimingsRequireRevalidation = source.TimingsRequireRevalidation,
                IsClone = true,
                Revision = 1,
                CreatedByUserId = userId,
                UpdatedByUserId = userId,
                CreatedAt = now,
                UpdatedAt = now
            };
            var entries = snapshot.Entries.Select(x => new SchoolTimetableEntry
            {
                SchoolId = source.SchoolId,
                InstructorProfileId = x.InstructorProfileId,
                Day = x.Day,
                Period = x.Period,
                EntryType = x.EntryType,
                IsStandbyLocked = x.EntryType == TimetableEntryType.Standby && (x.IsStandbyLocked ?? true),
                StandbySource = x.EntryType == TimetableEntryType.Standby ? x.StandbySource ?? "Manual" : null,
                ClassLabel = x.ClassLabel,
                Subject = x.Subject,
                ClassroomId = x.ClassroomId,
                SubjectId = x.SubjectId,
                ClassSubjectRequirementId = x.ClassSubjectRequirementId,
                RoomId = x.RoomId,
                CreatedAt = now,
                UpdatedAt = now
            }).ToList();

            await _repository.AddAsync(clone, ct);
            await _repository.SaveChangesAsync(ct);
            foreach (var entry in entries) entry.SchoolTimetableId = clone.Id;
            await _repository.AddEntriesAsync(entries, ct);
            await _repository.SaveChangesAsync(ct);
            await AddVersionAsync(clone, SnapshotEntries(entries), TimetableChangeKind.Cloned, null, ct,
                sourceTimetableId: source.Id, sourceVersionNumber: sourceVersion?.VersionNumber ??
                    await _repository.GetVersions(source.Id).Select(x => (int?)x.VersionNumber).MaxAsync(ct),
                audit: new ChangeAudit("تم إنشاء نسخة مستقلة", [new TimetableChangeDetailDto("clone", "مصدر النسخة", 1,
                    sourceVersion is null ? "الإصدار الحالي" : $"الإصدار {sourceVersion.VersionNumber}", title)]));
            await _repository.SaveChangesAsync(ct);
            cloneId = clone.Id;
        }, cancellationToken);

        return await GetByIdAsync(cloneId, cancellationToken);
    }

    public async Task<SchoolTimetableDto> RestoreAsync(
        int timetableId,
        int versionNumber,
        TimetableRevisionRequest request,
        CancellationToken cancellationToken = default)
    {
        var timetable = await RequireTrackedAsync(timetableId, cancellationToken);
        await EnsureManageAsync(timetable.SchoolId, cancellationToken);
        EnsureDraft(timetable);
        EnsureRevision(timetable, request.Revision);
        var version = await _repository.GetVersions(timetableId)
            .FirstOrDefaultAsync(x => x.VersionNumber == versionNumber, cancellationToken)
            ?? throw new KeyNotFoundException("نسخة الجدول المطلوبة غير موجودة.");
        var snapshot = JsonSerializer.Deserialize<TimetableSnapshotDto>(version.SnapshotJson)
            ?? throw new InvalidOperationException("تعذر قراءة نسخة الجدول المحفوظة.");
        if (snapshot.BellSchedule is not null)
            timetable.BellScheduleRevisionId = snapshot.BellSchedule.RevisionId;
        timetable.StandbySettingsJson = snapshot.StandbySettingsJson ?? "{}";
        var requests = snapshot.Entries.Select(x => new SaveTimetableEntryRequest(
            x.InstructorProfileId, x.Day, x.Period, x.EntryType, x.ClassLabel, x.Subject, x.ClassroomId, x.SubjectId, x.ClassSubjectRequirementId, x.RoomId,
            x.IsStandbyLocked, x.StandbySource)).ToList();
        var entries = await NormalizeAndValidateEntriesAsync(timetable, requests, cancellationToken);
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
        EnsureDraft(timetable);
        EnsureRevision(timetable, revision);
        var catalog = await GetCatalogAsync(timetable.SchoolId, cancellationToken);
        var imported = _documents.ParseImport(stream, catalog with { BellSchedule = (await GetByIdAsync(timetableId, cancellationToken)).BellSchedule });
        var requests = imported.Rows.SelectMany(x => x.Entries).ToList();
        var entries = await NormalizeAndValidateEntriesAsync(timetable, requests, cancellationToken);
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
        CancellationToken cancellationToken = default,
        bool personalOnly = true)
    {
        if (!Enum.IsDefined(colorMode))
            throw new ArgumentException("نمط ألوان ملف PDF غير صالح.", nameof(colorMode));
        var timetable = await GetByIdAsync(timetableId, cancellationToken, personalOnly);
        var catalog = await GetCatalogAsync(timetable.SchoolId, cancellationToken, personalOnly, timetable.TimetableSetupProfileId);
        return _documents.BuildPdf(timetable, catalog, colorMode);
    }

    private async Task ReplaceAsync(
        SchoolTimetable timetable,
        string title,
        IReadOnlyList<TimetableEntryDto> entries,
        TimetableChangeKind changeKind,
        int? restoredFromVersion,
        CancellationToken cancellationToken,
        bool allowPublished = false,
        int? setupRevision = null)
    {
        if (!allowPublished) EnsureDraft(timetable);
        var userId = RequireUserId();
        var now = DateTimeOffset.UtcNow;
        await _repository.ExecuteInTransactionAsync(async ct =>
        {
            var previousTitle = timetable.Title;
            var timing = timetable.BellScheduleRevisionId.HasValue
                ? await _timings.GetRevisionAsync(timetable.SchoolId, timetable.BellScheduleRevisionId.Value, ct) : null;
            var zone = TimeZoneInfo.FindSystemTimeZoneById(timing?.SchoolTimeZoneId ?? "Africa/Cairo");
            var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, zone).DateTime);
            if ((await _standbyRepository.GetProtectedCellsAsync(timetable.SchoolId, timetable.Id, today, ct)).Count > 0)
                throw new InvalidOperationException("توجد تغطية يومية مؤكدة مرتبطة بحصص هذا الجدول؛ راجع التكليفات قبل استبدال الحصص أو إعادة التوليد.");
            var previousEntries = SnapshotEntries(timetable.Entries);
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
                IsStandbyLocked = x.EntryType == TimetableEntryType.Standby && (x.IsStandbyLocked ?? true),
                StandbySource = x.EntryType == TimetableEntryType.Standby ? x.StandbySource ?? "Manual" : null,
                ClassLabel = x.ClassLabel,
                Subject = x.Subject,
                ClassroomId = x.ClassroomId,
                SubjectId = x.SubjectId,
                ClassSubjectRequirementId = x.ClassSubjectRequirementId,
                RoomId = x.RoomId,
                CreatedAt = now,
                UpdatedAt = now
            }).ToList();
            await _repository.AddEntriesAsync(newEntries, ct);
            timetable.Title = title;
            timetable.IsPublished = false;
            timetable.PublishedAt = null;
            timetable.PublishedByUserId = null;
            timetable.UpdatedAt = now;
            timetable.UpdatedByUserId = userId;
            if (setupRevision.HasValue)
            {
                timetable.SetupRevision = setupRevision;
                timetable.TimingsRequireRevalidation = false;
            }
            timetable.Revision++;
            await SaveWithConcurrencyMessageAsync(ct);
            await AddVersionAsync(timetable, entries, changeKind, restoredFromVersion, ct,
                audit: BuildChangeAudit(changeKind, previousTitle, previousEntries, title, entries));
            await _repository.SaveChangesAsync(ct);
        }, cancellationToken);
    }

    private static void EnsureDraft(SchoolTimetable timetable)
    {
        if (timetable.IsPublished)
            throw new InvalidOperationException("الجدول المنشور لقطة ثابتة؛ أنشئ مسودة جديدة لإجراء التعديلات.");
    }

    private async Task AddVersionAsync(
        SchoolTimetable timetable,
        IReadOnlyList<TimetableEntryDto> entries,
        TimetableChangeKind changeKind,
        int? restoredFromVersion,
        CancellationToken cancellationToken,
        string? checkpointName = null,
        string? checkpointNote = null,
        int? sourceTimetableId = null,
        int? sourceVersionNumber = null,
        ChangeAudit? audit = null)
    {
        var lastVersion = await _repository.GetVersions(timetable.Id)
            .Select(x => (int?)x.VersionNumber)
            .MaxAsync(cancellationToken) ?? 0;
        var timing = timetable.BellScheduleRevisionId.HasValue ? await _timings.GetRevisionAsync(timetable.SchoolId, timetable.BellScheduleRevisionId.Value, cancellationToken) : null;
        var snapshot = JsonSerializer.Serialize(new TimetableSnapshotDto(timetable.Title, entries, timing,
            timetable.TimetableSetupProfileId, timetable.SetupRevision, timetable.StandbySettingsJson));
        await _repository.AddVersionAsync(new SchoolTimetableVersion
        {
            SchoolTimetableId = timetable.Id,
            VersionNumber = lastVersion + 1,
            ChangeKind = changeKind,
            Title = timetable.Title,
            SnapshotJson = snapshot,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedByUserId = RequireUserId(),
            RestoredFromVersionNumber = restoredFromVersion,
            CheckpointName = checkpointName,
            CheckpointNote = checkpointNote,
            SourceTimetableId = sourceTimetableId,
            SourceVersionNumber = sourceVersionNumber,
            ChangeSummary = audit?.Summary ?? ChangeKindLabel(changeKind),
            ChangeDetailsJson = audit is null ? null : JsonSerializer.Serialize(audit.Details)
        }, cancellationToken);
    }

    private sealed record ChangeAudit(string Summary, IReadOnlyList<TimetableChangeDetailDto> Details);

    private static IReadOnlyList<TimetableChangeDetailDto> DeserializeChangeDetails(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return Array.Empty<TimetableChangeDetailDto>();
        try { return JsonSerializer.Deserialize<IReadOnlyList<TimetableChangeDetailDto>>(json) ?? []; }
        catch (JsonException) { return Array.Empty<TimetableChangeDetailDto>(); }
    }

    private static ChangeAudit BuildChangeAudit(
        TimetableChangeKind kind,
        string previousTitle,
        IReadOnlyList<TimetableEntryDto> previousEntries,
        string currentTitle,
        IReadOnlyList<TimetableEntryDto> currentEntries)
    {
        var before = previousEntries.ToDictionary(SlotKey, EntrySignature);
        var after = currentEntries.ToDictionary(SlotKey, EntrySignature);
        var added = after.Keys.Except(before.Keys).Count();
        var removed = before.Keys.Except(after.Keys).Count();
        var changed = before.Keys.Intersect(after.Keys).Count(key => !string.Equals(before[key], after[key], StringComparison.Ordinal));
        var details = new List<TimetableChangeDetailDto>();
        if (added > 0) details.Add(new("added", "حصص أضيفت", added));
        if (removed > 0) details.Add(new("removed", "حصص حُذفت", removed));
        if (changed > 0) details.Add(new("changed", "حصص عُدلت", changed));
        if (!string.Equals(previousTitle, currentTitle, StringComparison.Ordinal))
            details.Add(new("title", "عنوان الجدول", 1, previousTitle, currentTitle));
        if (details.Count == 0)
            details.Add(new("operation", ChangeKindLabel(kind), 1));

        var summary = kind switch
        {
            TimetableChangeKind.Saved => "تم حفظ تعديل يدوي",
            TimetableChangeKind.Regenerated => "تمت إعادة توليد الجدول",
            TimetableChangeKind.Imported => "تم استيراد جدول من Excel",
            TimetableChangeKind.Restored => "تم استرجاع إصدار سابق",
            _ => ChangeKindLabel(kind)
        };
        return new ChangeAudit(summary, details);
    }

    private static string SlotKey(TimetableEntryDto entry) => $"{entry.InstructorProfileId}:{entry.Day}:{entry.Period}";

    private static string EntrySignature(TimetableEntryDto entry) => JsonSerializer.Serialize(new
    {
        entry.EntryType, entry.ClassLabel, entry.Subject, entry.ClassroomId, entry.SubjectId,
        entry.ClassSubjectRequirementId, entry.RoomId, entry.IsStandbyLocked, entry.StandbySource
    });

    private static IReadOnlyList<TimetableEntryDto> SnapshotEntries(IEnumerable<SchoolTimetableEntry> entries) =>
        entries.Where(x => !x.IsDeleted).OrderBy(x => x.InstructorProfileId).ThenBy(x => x.Day).ThenBy(x => x.Period)
            .Select(x => new TimetableEntryDto(x.InstructorProfileId, x.Day, x.Period, x.EntryType,
                x.ClassLabel, x.Subject, x.ClassroomId, x.SubjectId, x.ClassSubjectRequirementId, x.RoomId,
                x.SubjectDefinition?.Color, x.Room?.Name, x.Id,
                x.EntryType == TimetableEntryType.Standby ? x.StandbySource == null || x.IsStandbyLocked : null, x.StandbySource)).ToArray();

    private static IReadOnlyList<TimetableEntryDto> SnapshotEntries(IEnumerable<TimetableEntryDto> entries) =>
        entries.Select(x => x with { Id = null }).ToArray();

    private async Task<List<TimetableEntryDto>> NormalizeAndValidateEntriesAsync(
        SchoolTimetable timetable,
        IReadOnlyList<SaveTimetableEntryRequest> requests,
        CancellationToken cancellationToken)
    {
        var schoolId = timetable.SchoolId;
        var timing = timetable.BellScheduleRevisionId.HasValue
            ? await _timings.GetRevisionAsync(schoolId, timetable.BellScheduleRevisionId.Value, cancellationToken) : null;
        if (timing is null) throw new ArgumentException("اختر قالب توقيت صالحاً في إعدادات الجدول أولاً.");
        var teacherIds = await _repository.GetTeachers(schoolId).Select(x => x.Id).ToListAsync(cancellationToken);
        var allowedTeachers = teacherIds.ToHashSet();
        var entries = new List<TimetableEntryDto>(requests.Count);
        foreach (var item in requests)
        {
            if (!allowedTeachers.Contains(item.InstructorProfileId))
                throw new ArgumentException("إحدى الخانات مرتبطة بمعلم غير نشط أو خارج المدرسة.");
            if (!Enum.IsDefined(item.Day)) throw new ArgumentException("يوم دراسي غير صالح.");
            if (!BellScheduleResolver.EffectivePeriods(timing, item.Day).Any(x => x.Sequence == item.Period))
                throw new ArgumentException($"الحصة {item.Period} غير موجودة في توقيت يوم {DayLabel(item.Day)}.");
            if (!Enum.IsDefined(item.EntryType)) throw new ArgumentException("نوع خانة الجدول غير صالح.");

            if (item.EntryType == TimetableEntryType.Standby)
            {
                var previous = timetable.Entries.FirstOrDefault(x => !x.IsDeleted && x.InstructorProfileId == item.InstructorProfileId &&
                    x.Day == item.Day && x.Period == item.Period && x.EntryType == TimetableEntryType.Standby);
                var source = item.StandbySource ?? previous?.StandbySource ?? "Manual";
                if (source is not ("Manual" or "Fair")) throw new ArgumentException("مصدر خانة الانتظار غير صالح.");
                entries.Add(new TimetableEntryDto(item.InstructorProfileId, item.Day, item.Period, item.EntryType, null, null,
                    IsStandbyLocked: item.IsStandbyLocked ?? (previous is null || previous.StandbySource is null || previous.IsStandbyLocked), StandbySource: source));
                continue;
            }

            var classLabel = NormalizeRequired(item.ClassLabel, 50, "الفصل");
            var subject = NormalizeRequired(item.Subject, 200, "المادة");
            entries.Add(new TimetableEntryDto(item.InstructorProfileId, item.Day, item.Period, item.EntryType, classLabel, subject,
                item.ClassroomId, item.SubjectId, item.ClassSubjectRequirementId, item.RoomId));
        }

        var duplicateTeacherSlot = entries.GroupBy(x => new { x.InstructorProfileId, x.Day, x.Period }).FirstOrDefault(x => x.Count() > 1);
        if (duplicateTeacherSlot != null)
            throw new ArgumentException("لا يمكن وضع أكثر من خانة للمعلم في اليوم والفترة نفسيهما.");

        // Subject validation resolves logical occurrences before checking classroom conflicts.
        await _availability.ValidateAssignmentsAsync(timetable, entries, cancellationToken);
        var validated = await _subjects.ValidateAsync(timetable, entries, timing, false, cancellationToken);
        if (entries.Any(x => x.EntryType == TimetableEntryType.Standby))
        {
            var context = await _standbyRepository.GetValidationContextAsync(schoolId, timetable.Id, cancellationToken) ?? throw new KeyNotFoundException();
            var simulated = TimetableRepairEngine.Simulate(context, []);
            simulated.Timetable.Entries = validated.Select((x, i) => new SchoolTimetableEntry { Id = i + 1, SchoolId = schoolId,
                SchoolTimetableId = timetable.Id, InstructorProfileId = x.InstructorProfileId, Day = x.Day, Period = x.Period,
                EntryType = x.EntryType, ClassLabel = x.ClassLabel, Subject = x.Subject, ClassroomId = x.ClassroomId,
                SubjectId = x.SubjectId, ClassSubjectRequirementId = x.ClassSubjectRequirementId, RoomId = x.RoomId }).ToList();
            var view = _standby.Inspect(simulated, new());
            if (!view.Ready) throw new ArgumentException(string.Join(" ", view.Issues));
        }
        return validated;
    }

    private async Task<SchoolTimetableDto> LoadDtoAsync(
        int timetableId,
        TimetableCapabilitiesDto capabilities,
        CancellationToken cancellationToken,
        bool personalOnly = true)
    {
        var currentInstructorUserId = ShouldLimitToCurrentInstructor(capabilities, personalOnly)
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
                    .Select(e => new TimetableEntryDto(e.InstructorProfileId, e.Day, e.Period, e.EntryType, e.ClassLabel, e.Subject, e.ClassroomId, e.SubjectId, e.ClassSubjectRequirementId, e.RoomId,
                        e.SubjectDefinition != null ? e.SubjectDefinition.Color : null,
                        e.Room != null ? e.Room.Name : null, e.Id,
                        e.EntryType == TimetableEntryType.Standby ? e.StandbySource == null || e.IsStandbyLocked : null, e.StandbySource))
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
        var header = await _repository.GetAll().Where(x => x.Id == timetableId)
            .Select(x => new { x.BellScheduleRevisionId, x.TimingsRequireRevalidation, x.TimetableSetupProfileId, x.IsClone }).SingleAsync(cancellationToken);
        var timing = header.BellScheduleRevisionId.HasValue ? await _timings.GetRevisionAsync(data.SchoolId, header.BellScheduleRevisionId.Value, cancellationToken) : null;
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
            capabilities, timing, header.TimingsRequireRevalidation, header.TimetableSetupProfileId, header.IsClone);
    }

    private bool ShouldLimitToCurrentInstructor(TimetableCapabilitiesDto capabilities, bool personalOnly) =>
        personalOnly && !capabilities.CanManage && _currentUser.IsInRole(RoleNames.Instructor);

    private async Task<TimetableCapabilitiesDto> GetCapabilitiesAsync(int schoolId, CancellationToken cancellationToken)
    {
        var canDelegate = _currentUser.IsGlobalAdmin() || _currentUser.IsInRole(RoleNames.SchoolManager);
        var canManage = canDelegate || (!_currentUser.IsInRole(RoleNames.Instructor) && !_currentUser.IsInRole(RoleNames.Guardian) && _currentUser.HasPermission(PermissionNames.TimetableManage));
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

    private static string? NormalizeOptional(string? value, int maxLength)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized.Length > maxLength
            ? throw new ArgumentException($"Ø§Ù„Ù†Øµ Ù„Ø§ ÙŠÙ…ÙƒÙ† Ø£Ù† ÙŠØªØ¬Ø§ÙˆØ² {maxLength} Ø­Ø±ÙÙ‹Ø§.")
            : normalized;
    }

    private static TimetableEntryDto ToDto(SchoolTimetableEntry entry) => new(
        entry.InstructorProfileId,
        entry.Day,
        entry.Period,
        entry.EntryType,
        entry.ClassLabel,
        entry.Subject, entry.ClassroomId, entry.SubjectId, entry.ClassSubjectRequirementId, entry.RoomId, entry.SubjectDefinition?.Color, entry.Room?.Name);

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
        TimetableDay.Friday => "الجمعة",
        _ => day.ToString()
    };

    private static string ChangeKindLabel(TimetableChangeKind kind) => kind switch
    {
        TimetableChangeKind.Created => "إنشاء",
        TimetableChangeKind.Saved => "حفظ",
        TimetableChangeKind.Published => "نشر",
        TimetableChangeKind.Imported => "استيراد Excel",
        TimetableChangeKind.Restored => "استرجاع نسخة",
        TimetableChangeKind.Regenerated => "إعادة توليد",
        TimetableChangeKind.Checkpoint => "نقطة استعادة",
        TimetableChangeKind.Cloned => "نسخ مستقل",
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
