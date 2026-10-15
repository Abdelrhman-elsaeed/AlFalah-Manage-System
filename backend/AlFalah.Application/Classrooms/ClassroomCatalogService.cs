using AlFalah.Application.Common;
using AlFalah.Application.Interfaces;
using AlFalah.Domain.Entities;
using AlFalah.Domain.Entities.StudentAffairs;
using AlFalah.Domain.Enums;

namespace AlFalah.Application.Classrooms;

public sealed class ClassroomCatalogService(IClassroomCatalogRepository repository, ICurrentUserService user) : IClassroomCatalogService
{
    private int School => user.ActiveSchoolId ?? throw Denied();
    private string Actor => user.UserId ?? throw Denied();
    private static UnauthorizedSchoolAccessException Denied() => new("لا تملك صلاحية الوصول إلى فصول هذه المدرسة.");
    private async Task<CatalogContext> Context(CancellationToken ct)
    {
        if (!user.IsAuthenticated) throw Denied();
        var context = await repository.ContextAsync(School, Actor, ct);
        if (!context.IsMember && !user.IsGlobalAdmin()) throw Denied();
        return context;
    }
    private static int CurrentYear(CatalogContext context) => context.Years.Where(y => y.IsActive).Select(y => y.Id).Distinct().ToArray() is [var id]
        ? id : throw new ArgumentException("حدّد عامًا دراسيًا نشطًا واحدًا للمدرسة قبل اختيار الفصول.");
    public static ClassroomOption Option(Classroom c) => new(c.Id, c.ClassLabel, c.Stage, c.GradeLevel, c.Section,
        c.SectionNumber, c.AcademicYearId, c.IsActive, c.PhysicalLocation, c.UpdatedAt.ToString("O"));
    public async Task<ClassroomOptionsPage> OptionsAsync(int? year, int page, bool includeInactive, CancellationToken ct, DateOnly? onDate = null)
    {
        var context = await Context(ct);
        var selected = onDate.HasValue ? YearAt(context, onDate.Value) : year ?? CurrentYear(context);
        if (year.HasValue && selected != year.Value) throw new ArgumentException("العام الدراسي لا يطابق تاريخ الزيارة.");
        if (page < 1 || page > 100000 || context.Years.All(y => y.Id != selected)) throw new ArgumentException("العام أو الصفحة غير صالحين.");
        var rows = await repository.PageAsync(School, selected, page, includeInactive, ct);
        return new(context, rows.Take(100).Select(Option).ToArray(), rows.Count > 100);
    }
    public static int YearAt(CatalogContext context, DateOnly date)
    {
        var years = context.Years.Where(y => y.StartsOn <= date && y.EndsOn >= date).Select(y => y.Id).Distinct().ToArray();
        return years is [var id] ? id : throw new ArgumentException("لا يوجد عام دراسي واحد محدد للمدرسة في هذا التاريخ. راجع إعدادات الأعوام الدراسية.");
    }
    public async Task<IReadOnlyList<ClassroomOption>> CreateAsync(ClassroomBatch batch, CancellationToken ct)
    {
        var context = await Context(ct);
        if (!context.IsManager && !user.IsGlobalAdmin() && !user.HasPermission(PermissionNames.ClassroomManage) && !user.HasPermission(PermissionNames.StudentEnrollmentManage)) throw Denied();
        if (context.Years.All(y => y.Id != batch.AcademicYearId || !y.IsActive)) throw new ArgumentException("إضافة الفصول متاحة للعام النشط فقط.");
        _ = ClassroomIdentity.GradeLabel(context.Stage, batch.GradeLevel);
        if (batch.Sections is null || batch.Sections.Count is < 1 or > 100 || string.IsNullOrWhiteSpace(batch.PhysicalLocation) || batch.PhysicalLocation.Trim().Length > 120)
            throw new ArgumentException("اختر من شعبة إلى ١٠٠ شعبة، وحدّد موقع الفصول.");
        var sections = batch.Sections.Select(ClassroomIdentity.Section).ToArray();
        if (sections.Distinct().Count() != sections.Length) throw new ArgumentException("أرقام الشعب مكررة؛ لم تتم إضافة أي فصل.");
        var existing = await repository.ClassroomsAsync(School, batch.AcademicYearId, ct);
        var labels = await repository.LabelsAsync(School, batch.AcademicYearId, ct);
        if (sections.Any(s => labels.Any(l => string.Equals(l.Label, ClassroomIdentity.Label(context.Stage, batch.GradeLevel, s), StringComparison.OrdinalIgnoreCase))))
            throw new ArgumentException("اسم إحدى الشعب مرتبط بسجل فصل سابق. لا يمكن إعادة استخدامه لفصل آخر.");
        if (existing.Any(c => c.Stage == context.Stage && c.GradeLevel == batch.GradeLevel && sections.Contains(c.SectionNumber ?? NumericSection(c.Section))))
            throw new ArgumentException("إحدى الشعب موجودة بالفعل؛ لم تتم إضافة أي فصل.");
        var now = DateTimeOffset.UtcNow;
        var rows = sections.Select(section => new Classroom { SchoolId = School, AcademicYearId = batch.AcademicYearId,
            Stage = context.Stage, GradeLevel = (byte)batch.GradeLevel, SectionNumber = section, Section = section.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ClassLabel = ClassroomIdentity.Label(context.Stage, batch.GradeLevel, section), PhysicalLocation = batch.PhysicalLocation.Trim(),
            CreatedAt = now, UpdatedAt = now, CreatedByUserId = Actor, UpdatedByUserId = Actor }).ToArray();
        repository.Audit(new AuditLog { SchoolId = School, UserId = Actor, Action = "Classroom.BatchCreated", EntityName = nameof(Classroom),
            NewValues = System.Text.Json.JsonSerializer.Serialize(new { batch.AcademicYearId, batch.GradeLevel, Sections = sections }) });
        await repository.AddAsync(rows, ct); return rows.Select(Option).ToArray();
    }
    private static int NumericSection(string section) { try { return ClassroomIdentity.Section(section); } catch (ArgumentException) { return -1; } }

    public async Task<ClassroomOption> UpdateAsync(int id, ClassroomEdit edit, CancellationToken ct)
    {
        var context = await Context(ct);
        if (!context.IsManager && !user.IsGlobalAdmin() && !user.HasPermission(PermissionNames.ClassroomManage) && !user.HasPermission(PermissionNames.StudentEnrollmentManage)) throw Denied();
        var row = await repository.ForUpdateAsync(School, id, ct) ?? throw new KeyNotFoundException();
        if (context.Years.All(y => y.Id != row.AcademicYearId || !y.IsActive)) throw new ArgumentException("العام السابق للقراءة فقط.");
        if (row.UpdatedAt.ToString("O") != edit.Revision) throw new ClassroomCatalogConflictException();
        var section = ClassroomIdentity.Section(edit.Section);
        var label = ClassroomIdentity.Label(row.Stage, row.GradeLevel, section);
        var labels = await repository.LabelsAsync(School, row.AcademicYearId, ct);
        if (labels.Any(l => l.ClassroomId != id && string.Equals(l.Label, label, StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("هذا الاسم مرتبط بفصل آخر في السجل التاريخي؛ اختر رقم شعبة مختلفًا.");
        if (string.IsNullOrWhiteSpace(edit.PhysicalLocation) || edit.PhysicalLocation.Trim().Length > 120) throw new ArgumentException("حدّد موقع الفصل.");
        var classes = await repository.ClassroomsAsync(School, row.AcademicYearId, ct);
        if (classes.Any(c => c.Id != id && c.Stage == row.Stage && c.GradeLevel == row.GradeLevel && (c.SectionNumber ?? NumericSection(c.Section)) == section))
            throw new ArgumentException("رقم الشعبة موجود لهذا الصف بالفعل.");
        var old = new { row.ClassLabel, row.Section, row.IsActive, row.PhysicalLocation };
        foreach (var retained in new[] {row.ClassLabel, label}.Distinct(StringComparer.OrdinalIgnoreCase))
            if (!labels.Any(l => string.Equals(l.Label, retained, StringComparison.OrdinalIgnoreCase)))
                repository.AddLabel(new() { SchoolId = School, AcademicYearId = row.AcademicYearId, ClassroomId = id, Label = retained });
        row.SectionNumber = section; row.Section = section.ToString(System.Globalization.CultureInfo.InvariantCulture);
        row.ClassLabel = label; row.IsActive = edit.IsActive;
        row.PhysicalLocation = edit.PhysicalLocation.Trim(); row.UpdatedAt = DateTimeOffset.UtcNow; row.UpdatedByUserId = Actor;
        repository.Audit(new AuditLog { SchoolId = School, UserId = Actor, Action = "Classroom.Updated", EntityName = nameof(Classroom),
            EntityId = id.ToString(), OldValues = System.Text.Json.JsonSerializer.Serialize(old), NewValues = System.Text.Json.JsonSerializer.Serialize(Option(row)) });
        await repository.SaveAsync(ct); return Option(row);
    }

    // Validates the complete selection before mutating tracked entities. The caller saves
    // profile fields, association rows and its audit in the existing unit of work.
    public async Task ApplyTeacherSelectionAsync(InstructorProfile profile, IReadOnlyList<int>? ids, IReadOnlyList<string>? labels,
        int? year, int? revision, CancellationToken ct)
    {
        if (ids is null && labels is null) return;
        if (!user.IsAuthenticated || string.IsNullOrWhiteSpace(user.UserId)) throw Denied();
        if (!user.IsGlobalAdmin() && profile.SchoolId != School) throw Denied();
        var school = profile.SchoolId;
        var context = await repository.ContextAsync(school, Actor, ct);
        if (!context.IsMember && !user.IsGlobalAdmin()) throw Denied();
        if (Actor != profile.UserId && !context.IsManager && !user.IsGlobalAdmin() && !user.HasPermission(PermissionNames.UserEdit)) throw Denied();
        var rows = (await repository.AssociationsAsync(profile.Id, ct)).Where(r => r.SchoolId == null || r.SchoolId == school).ToArray();
        if (ids is null && labels!.Count == 0 && rows.Length == 0) return;
        var current = CurrentYear(context);
        if (year.HasValue && year != current) throw new ArgumentException("اختيار فصول المعلم متاح للعام النشط فقط.");
        if (ids is not null && (!revision.HasValue || revision != profile.TeachingRevision))
            throw new ClassroomCatalogConflictException();
        var classes = await repository.ClassroomsAsync(school, current, ct);
        var selected = new List<Classroom>();
        var retainedLegacy = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (ids is not null)
        {
            if (ids.Count > 5000 || ids.Distinct().Count() != ids.Count) throw new ArgumentException("اختيار الفصول مكرر أو غير صالح.");
            foreach (var id in ids) selected.Add(classes.SingleOrDefault(c => c.Id == id && (c.IsActive || rows.Any(r => r.ClassroomId == id && r.SchoolId == school))) ?? throw new ArgumentException("أحد الفصول غير نشط أو لا ينتمي إلى المدرسة والعام الحاليين."));
        }
        else foreach (var label in labels!.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var matches = classes.Where(c => c.IsActive && c.ClassLabel == label).ToArray();
            if (matches.Length == 1) selected.Add(matches[0]);
            else if (rows.Any(r => r.ClassroomId is null && r.ClassLabel == label)) retainedLegacy.Add(label);
            else throw new ArgumentException("اختر الفصول من سجل المدرسة؛ لا يمكن إضافة اسم فصل يدويًا.");
        }
        // Older user forms may echo their existing labels. They cannot replace
        // a revisioned ID set through an unversioned text payload.
        if (ids is null)
        {
            var existingIds = rows.Where(r => r.ClassroomId.HasValue && classes.Any(c => c.Id == r.ClassroomId))
                .Select(r => r.ClassroomId!.Value).ToHashSet();
            var existingLegacy = rows.Where(r => r.ClassroomId is null).Select(r => r.ClassLabel).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (existingIds.SetEquals(selected.Select(c => c.Id)) && existingLegacy.SetEquals(retainedLegacy)) return;
            throw new ArgumentException("لتغيير فصول المعلم، استخدم قائمة فصول المدرسة وأرسل معرّفاتها وإصدار البيانات الحالي.");
        }
        var now = DateTimeOffset.UtcNow;
        foreach (var row in rows)
        {
            if (row.ClassroomId.HasValue && classes.All(c => c.Id != row.ClassroomId)) continue; // Retain prior-year associations.
            if (row.ClassroomId.HasValue && selected.Any(c => c.Id == row.ClassroomId)) continue;
            if (row.ClassroomId is null && retainedLegacy.Contains(row.ClassLabel)) continue;
            row.IsDeleted = true; row.DeletedAt = now; row.DeletedByUserId = Actor;
        }
        for (var i = 0; i < selected.Count; i++)
        {
            var classroom = selected[i];
            var row = rows.FirstOrDefault(r => !r.IsDeleted && r.ClassroomId == classroom.Id)
                ?? rows.FirstOrDefault(r => !r.IsDeleted && r.ClassroomId is null && r.ClassLabel == classroom.ClassLabel);
            if (row is null) { row = new InstructorClass { InstructorProfile = profile, InstructorProfileId = profile.Id, SchoolId = school, ClassLabel = classroom.ClassLabel }; repository.AddAssociation(row); }
            row.ClassroomId = classroom.Id; row.SchoolId = school; row.SortOrder = i; row.UpdatedAt = now;
        }
        repository.Audit(new AuditLog { SchoolId = school, UserId = Actor, Action = "Classroom.TeacherSelection", EntityName = nameof(InstructorProfile),
            EntityId = profile.Id == 0 ? profile.UserId : profile.Id.ToString(), Reason = Actor == profile.UserId ? "Self" : "Manager",
            NewValues = System.Text.Json.JsonSerializer.Serialize(new { AcademicYearId = current, ClassroomIds = selected.Select(c => c.Id), Revision = profile.TeachingRevision + 1 }) });
        profile.TeachingRevision++;
    }
}
public sealed class ClassroomCatalogConflictException : Exception
{
    public ClassroomCatalogConflictException() : base("تغيّرت فصول المعلم. حدّث البيانات ثم أعد اختيارك.") { }
}
