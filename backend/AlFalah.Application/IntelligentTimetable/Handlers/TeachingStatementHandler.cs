using AlFalah.Application.Interfaces;
using AlFalah.Domain.Enums;
using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.IntelligentTimetable.Handlers;

public sealed class TeachingStatementHandler(TeachingStatementService service, ICurrentUserService user) : IRequestHandler<GetTeachingStatementQuery,ApiResponse<TeachingStatementFile>>
{
    public async Task<ApiResponse<TeachingStatementFile>> Handle(GetTeachingStatementQuery query,CancellationToken ct)
    {
        if(!user.IsAuthenticated || string.IsNullOrWhiteSpace(user.UserId) || !user.ActiveSchoolId.HasValue ||
            TimetableSettingsHandlerSupport.IsExcludedRole(user) || !user.HasPermission(PermissionNames.TimetableManage))
            return ApiResponse<TeachingStatementFile>.Fail(TimetableSettingsHandlerSupport.PermissionDenied);
        try {return ApiResponse<TeachingStatementFile>.Success(await service.GetAsync(user.ActiveSchoolId.Value,query.SetupId,query.Revision,query.Scope,user.UserId,ct));}
        catch(KeyNotFoundException){return ApiResponse<TeachingStatementFile>.Fail(TimetableSettingsHandlerSupport.NotFound);}
        catch(BellScheduleConflictException){return ApiResponse<TeachingStatementFile>.Fail(TimetableSettingsHandlerSupport.ConcurrencyConflict);}
        catch(ArgumentException ex){return ApiResponse<TeachingStatementFile>.Fail(ex.Message);}
    }
}
