using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AlFalah.Application.DTOs.ImprovementPlans;

namespace AlFalah.Application.Interfaces;

public interface IImprovementPlanService
{
    Task<List<ImprovementPlanListItemDto>> GetPlansAsync(CancellationToken cancellationToken = default);
    Task<List<ImprovementPlanDto>> GetPlansForVisitAsync(int visitId, CancellationToken cancellationToken = default);
    Task<ImprovementPlanDto> GetPlanByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<ImprovementPlanDto> CreatePlanAsync(CreatePlanRequestDto request, CancellationToken cancellationToken = default);
    Task<ImprovementPlanDto> UpdatePlanAsync(int id, UpdatePlanRequestDto request, CancellationToken cancellationToken = default);
    Task<ImprovementPlanDto> ReactivatePlanAsync(int id, CancellationToken cancellationToken = default);
    Task SoftDeletePlanAsync(int id, CancellationToken cancellationToken = default);
    
    Task<List<WeakDomainSuggestionDto>> GetWeakDomainSuggestionsAsync(int visitId, CancellationToken cancellationToken = default);
    
    Task<PlanFollowUpDto> AddFollowUpAsync(int planId, CreateFollowUpRequestDto request, CancellationToken cancellationToken = default);
    Task<PlanFollowUpDto> UpdateFollowUpAsync(int id, UpdateFollowUpRequestDto request, CancellationToken cancellationToken = default);
    Task SoftDeleteFollowUpAsync(int id, CancellationToken cancellationToken = default);
    
    Task<PlanProgressDto> GetPlanProgressAsync(int planId, CancellationToken cancellationToken = default);
}
