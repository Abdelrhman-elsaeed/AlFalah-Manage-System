using AlFalah.Application.StudentAffairs.DTOs.Behaviors;
using AlFalah.Application.StudentAffairs.DTOs.Delays;
using AlFalah.Application.StudentAffairs.DTOs.Recognitions;
using AlFalah.Shared.Models;

namespace AlFalah.Application.StudentAffairs.OfficerOperations;

public interface IOfficerOperationalReadRepository
{
    Task<PagedResult<AcademicConcernDto>> GetAcademicConcernsAsync(int schoolId, AcademicConcernListQuery query, CancellationToken cancellationToken);
    Task<PagedResult<BehaviorIncidentDto>> GetBehaviorIncidentsAsync(int schoolId, BehaviorListQuery query, CancellationToken cancellationToken);
    Task<PagedResult<MorningDelayDto>> GetMorningDelaysAsync(int schoolId, MorningDelayListQuery query, CancellationToken cancellationToken);
    Task<PagedResult<SessionDelayDto>> GetSessionDelaysAsync(int schoolId, SessionDelayListQuery query, CancellationToken cancellationToken);
    Task<PagedResult<RecognitionDto>> GetRecognitionsAsync(int schoolId, RecognitionListQuery query, CancellationToken cancellationToken);
    Task<MorningDelayDto?> GetMorningDelayAsync(int schoolId, int delayId, CancellationToken cancellationToken);
}
