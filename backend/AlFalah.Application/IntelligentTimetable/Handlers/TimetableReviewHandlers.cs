using AlFalah.Application.IntelligentTimetable.DTOs;
using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.IntelligentTimetable.Handlers;

public sealed class TimetableReviewHandlers(TimetableReviewService service) :
    IRequestHandler<GetReviewTimetablesQuery, ApiResponse<IReadOnlyList<ReviewTimetableOption>>>,
    IRequestHandler<EvaluateTimetableQuery, ApiResponse<TimetableReviewResultDto>>,
    IRequestHandler<GetRepairProposalsQuery, ApiResponse<List<RepairProposalDto>>>,
    IRequestHandler<OverrideSoftViolationCommand, ApiResponse<bool>>,
    IRequestHandler<ApplyRepairProposalCommand, ApiResponse<TimetableReviewResultDto>>
{
    public Task<ApiResponse<IReadOnlyList<ReviewTimetableOption>>> Handle(GetReviewTimetablesQuery q, CancellationToken ct) => Respond(() => service.ListAsync(ct));
    public Task<ApiResponse<TimetableReviewResultDto>> Handle(EvaluateTimetableQuery q, CancellationToken ct) => Respond(() => service.EvaluateAsync(q.TimetableId, ct));
    public Task<ApiResponse<List<RepairProposalDto>>> Handle(GetRepairProposalsQuery q, CancellationToken ct) => Respond(() => service.ProposalsAsync(q.TimetableId, q.FindingId, ct));
    public Task<ApiResponse<bool>> Handle(OverrideSoftViolationCommand q, CancellationToken ct) => Respond(() => service.OverrideAsync(q.FindingId, q.Reason, ct));
    public Task<ApiResponse<TimetableReviewResultDto>> Handle(ApplyRepairProposalCommand q, CancellationToken ct) => Respond(() => service.ApplyAsync(q.TimetableId, q.Proposal, ct));
    private static async Task<ApiResponse<T>> Respond<T>(Func<Task<T>> action)
    {
        try { return ApiResponse<T>.Success(await action()); }
        catch (UnauthorizedAccessException) { return ApiResponse<T>.Fail(TimetableSettingsHandlerSupport.PermissionDenied); }
        catch (KeyNotFoundException) { return ApiResponse<T>.Fail(TimetableSettingsHandlerSupport.NotFound); }
        catch (BellScheduleConflictException) { return ApiResponse<T>.Fail(TimetableSettingsHandlerSupport.ConcurrencyConflict); }
        catch (ArgumentException ex) { return ApiResponse<T>.Fail(ex.Message); }
    }
}
