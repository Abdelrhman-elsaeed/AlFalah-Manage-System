using AlFalah.Application.DTOs.Dashboards;

namespace AlFalah.Application.Interfaces;

public interface ISchoolManagerOperationsService
{
    Task<SchoolManagerOperationsDto> GetAsync(CancellationToken cancellationToken = default);
}

public sealed record SchoolManagerOperationsQuery(int SchoolId, DateOnly Date,
    DateTimeOffset DayStartsAt, bool IncludeStudents, bool IncludeGatePasses, bool IncludeConversations);

public interface ISchoolManagerOperationsRepository
{
    Task<bool> IsActiveManagerAsync(int schoolId, string userId, CancellationToken cancellationToken);
    Task<SchoolManagerOperationsDto> ReadAsync(SchoolManagerOperationsQuery query, CancellationToken cancellationToken);
}
