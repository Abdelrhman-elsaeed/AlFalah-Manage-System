using AlFalah.Application.IntelligentTimetable.DTOs;
using AlFalah.Domain.Entities;
using AlFalah.Domain.Entities.StudentAffairs;
using AlFalah.Domain.Enums;

namespace AlFalah.Application.IntelligentTimetable;

public sealed record TimetableAssignmentMemberReadinessData(
    int TeacherTimetableProfileId,
    int AllocatedPeriodCount,
    int AllocatedPairedBlockCount,
    bool IsTeacherReady);

public sealed record TimetableAssignmentReadinessData(
    int RequirementId,
    string Mode,
    int IndividualPeriodCount,
    int PairedBlockCount,
    IReadOnlyList<TimetableAssignmentMemberReadinessData> Members);

public sealed record TimetableReadinessData(
    int ActiveClassrooms,
    int ActiveStudents,
    int ClassroomsMissingLocation,
    int ActiveTeachers,
    int ConfiguredTeachers,
    int AvailableSubjects,
    int CoveredClassrooms,
    int RequirementCount,
    IReadOnlyList<TimetableAssignmentReadinessData> Assignments,
    bool TimetableExists,
    bool HasCurrentTimetable);

public interface ITimetableSettingsRepository
{
    Task<string?> GetSchoolNameAsync(int schoolId, CancellationToken cancellationToken);
    Task<IReadOnlyList<TimetableSetupAcademicYearDto>> GetAcademicYearsAsync(int schoolId, CancellationToken cancellationToken);
    Task<AcademicYear?> GetAcademicYearByCodeForUpdateAsync(string normalizedCode, CancellationToken cancellationToken);
    Task<IReadOnlyList<AcademicTerm>> GetAcademicTermsForUpdateAsync(int schoolId, CancellationToken cancellationToken);
    Task<IReadOnlyList<TimetableSetupProfileDto>> GetProfilesAsync(
        int schoolId,
        int academicYearId,
        TimetableSemester semester,
        CancellationToken cancellationToken);
    Task<TimetableSetupProfileDto?> GetProfileDtoAsync(int schoolId, int profileId, CancellationToken cancellationToken);
    Task<TimetableSetupProfile?> GetProfileForUpdateAsync(int schoolId, int profileId, CancellationToken cancellationToken);
    Task<TimetableReadinessData> GetReadinessDataAsync(
        int schoolId,
        int academicYearId,
        TimetableSemester semester,
        int? profileId,
        CancellationToken cancellationToken);
    Task<bool> AcademicScopeExistsAsync(
        int schoolId,
        int academicYearId,
        TimetableSemester semester,
        CancellationToken cancellationToken);
    Task<bool> ProfileNameExistsAsync(
        int schoolId,
        int academicYearId,
        TimetableSemester semester,
        string normalizedName,
        int? excludingProfileId,
        CancellationToken cancellationToken);
    Task<bool> HasEditorGrantAsync(int schoolId, string userId, CancellationToken cancellationToken);
    void Add(AcademicYear academicYear);
    void Add(AcademicTerm academicTerm);
    void Add(TimetableSetupProfile profile);
    void WriteAcademicScopeAudit(
        int schoolId,
        string userId,
        AcademicYear academicYear,
        TimetableSemester activeSemester);
    void WriteAudit(
        int schoolId,
        string userId,
        string action,
        TimetableSetupProfile profile,
        object? oldValues,
        object? newValues);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken);
}
