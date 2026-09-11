using AlFalah.Application.IntelligentTimetable.DTOs;
using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.IntelligentTimetable.Handlers;

public sealed class TimetableSubstitutionHandlers(TimetableSubstitutionService service) :
    IRequestHandler<GetSubstitutionTimetablesQuery, ApiResponse<IReadOnlyList<ReviewTimetableOption>>>,
    IRequestHandler<GetDailySubstitutionsQuery, ApiResponse<DailySubstitutionDto>>,
    IRequestHandler<GetSwapCandidatesQuery, ApiResponse<SwapCandidatesDto>>,
    IRequestHandler<ExecuteSwapCommand, ApiResponse<SubstitutionHistoryDto>>
{
    public Task<ApiResponse<IReadOnlyList<ReviewTimetableOption>>> Handle(GetSubstitutionTimetablesQuery q, CancellationToken ct) => Respond(() => service.ListAsync(ct));
    public Task<ApiResponse<DailySubstitutionDto>> Handle(GetDailySubstitutionsQuery q, CancellationToken ct) => Respond(() => service.DailyAsync(q.TimetableId, q.Date, ct));
    public Task<ApiResponse<SwapCandidatesDto>> Handle(GetSwapCandidatesQuery q, CancellationToken ct) => Respond(() => service.CandidatesAsync(q.TimetableId, q.Date, q.SourceEntryId, q.Mode, ct));
    public Task<ApiResponse<SubstitutionHistoryDto>> Handle(ExecuteSwapCommand q, CancellationToken ct) => Respond(() => service.ExecuteAsync(q.TimetableId, q.Request, ct));
    private static async Task<ApiResponse<T>> Respond<T>(Func<Task<T>> action)
    {
        try { return ApiResponse<T>.Success(await action()); }
        catch (UnauthorizedAccessException) { return ApiResponse<T>.Fail(TimetableSettingsHandlerSupport.PermissionDenied); }
        catch (KeyNotFoundException) { return ApiResponse<T>.Fail(TimetableSettingsHandlerSupport.NotFound); }
        catch (BellScheduleConflictException) { return ApiResponse<T>.Fail(TimetableSettingsHandlerSupport.ConcurrencyConflict); }
        catch (ArgumentException ex) { return ApiResponse<T>.Fail(ex.Message); }
    }
}
