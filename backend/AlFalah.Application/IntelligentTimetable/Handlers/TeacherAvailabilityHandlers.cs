using AlFalah.Application.IntelligentTimetable.DTOs;
using AlFalah.Application.Interfaces;
using AlFalah.Application.Validators.IntelligentTimetable;
using AlFalah.Domain.Enums;
using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.IntelligentTimetable.Handlers;

public sealed class TeacherAvailabilityHandlers(ITeacherAvailabilityRepository repository, TeacherAvailabilityService service,
    ICurrentUserService user, TimeProvider clock) :
    IRequestHandler<GetAvailabilityTeachersQuery, ApiResponse<IReadOnlyList<AvailabilityTeacherDto>>>,
    IRequestHandler<GetTeacherAvailabilityQuery, ApiResponse<TeacherAvailabilityDto>>,
    IRequestHandler<UpdateTeacherProfileCommand, ApiResponse<TeacherAvailabilityDto>>
{
    private bool Allowed => user.IsAuthenticated && !string.IsNullOrWhiteSpace(user.UserId) && user.ActiveSchoolId.HasValue &&
        !TimetableSettingsHandlerSupport.IsExcludedRole(user) && user.HasPermission(PermissionNames.TimetableManage);

    public async Task<ApiResponse<IReadOnlyList<AvailabilityTeacherDto>>> Handle(GetAvailabilityTeachersQuery query, CancellationToken ct)
    {
        if (!Allowed) return ApiResponse<IReadOnlyList<AvailabilityTeacherDto>>.Fail(TimetableSettingsHandlerSupport.PermissionDenied);
        if (await repository.GetSetupAsync(user.ActiveSchoolId!.Value, query.SetupId, ct) is null)
            return ApiResponse<IReadOnlyList<AvailabilityTeacherDto>>.Fail(TimetableSettingsHandlerSupport.NotFound);
        return ApiResponse<IReadOnlyList<AvailabilityTeacherDto>>.Success(await repository.GetTeachersAsync(user.ActiveSchoolId.Value, ct));
    }
    public Task<ApiResponse<TeacherAvailabilityDto>> Handle(GetTeacherAvailabilityQuery query, CancellationToken ct) =>
        Respond(() => service.GetAsync(user.ActiveSchoolId!.Value, query.SetupId, query.TeacherId, ct));

    public async Task<ApiResponse<TeacherAvailabilityDto>> Handle(UpdateTeacherProfileCommand command, CancellationToken ct)
    {
        if (!Allowed) return ApiResponse<TeacherAvailabilityDto>.Fail(TimetableSettingsHandlerSupport.PermissionDenied);
        var validation = await new TeacherAvailabilityValidator().ValidateAsync(command.Request, ct);
        if (!validation.IsValid) return ApiResponse<TeacherAvailabilityDto>.Fail(validation.Errors.Select(x => x.ErrorMessage).ToList());
        return await Respond(() => service.UpdateAsync(user.ActiveSchoolId!.Value, command.SetupId, command.TeacherId,
            command.Request, user.UserId!, clock.GetUtcNow(), ct));
    }
    private async Task<ApiResponse<TeacherAvailabilityDto>> Respond(Func<Task<TeacherAvailabilityDto>> action)
    {
        if (!Allowed) return ApiResponse<TeacherAvailabilityDto>.Fail(TimetableSettingsHandlerSupport.PermissionDenied);
        try { return ApiResponse<TeacherAvailabilityDto>.Success(await action()); }
        catch (KeyNotFoundException) { return ApiResponse<TeacherAvailabilityDto>.Fail(TimetableSettingsHandlerSupport.NotFound); }
        catch (BellScheduleConflictException) { return ApiResponse<TeacherAvailabilityDto>.Fail(TimetableSettingsHandlerSupport.ConcurrencyConflict); }
        catch (ArgumentException ex) { return ApiResponse<TeacherAvailabilityDto>.Fail(ex.Message); }
    }
}
