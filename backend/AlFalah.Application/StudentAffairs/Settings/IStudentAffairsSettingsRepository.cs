using AlFalah.Application.StudentAffairs.DTOs.Settings;
using AlFalah.Application.StudentAffairs.DTOs.Shared;
using AlFalah.Domain.Entities.StudentAffairs;
using AlFalah.Shared.Models;

namespace AlFalah.Application.StudentAffairs.Settings;

public interface IStudentAffairsSettingsRepository
{
    Task<SchoolStudentAffairsSettings?> GetSettingsAsync(int schoolId, CancellationToken cancellationToken);
    Task<SchoolStudentAffairsSettings?> GetSettingsForUpdateAsync(int schoolId, CancellationToken cancellationToken);
    Task<SchoolStudentAffairsSettingsDto?> GetSettingsDtoAsync(int schoolId, CancellationToken cancellationToken);
    Task<PagedResult<StudentAffairsSettingsHistoryDto>> GetHistoryAsync(int schoolId, StudentAffairsPageQuery query, CancellationToken cancellationToken);
    void AddSettings(SchoolStudentAffairsSettings settings);
    void SetExpectedRowVersion(SchoolStudentAffairsSettings settings, byte[] rowVersion);
    void WriteAudit(int schoolId, string userId, string action, string? entityId, string? reason, object? oldValues, object? newValues);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
