using AlFalah.Application.Interfaces;
using AlFalah.Application.StudentAffairs.DTOs.GatePasses;
using AlFalah.Domain.Entities.StudentAffairs;
using AlFalah.Domain.Enums;
using AlFalah.Domain.Enums.StudentAffairs;
using AlFalah.Domain.Events;
using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.StudentAffairs.GatePasses.Handlers;

public sealed class CreateGatePassCommandHandler
    : IRequestHandler<CreateGatePassCommand, ApiResponse<GatePassDto>>
{
    private static readonly TimeSpan OverlapTolerance = TimeSpan.FromMinutes(30);
    private readonly IGatePassWorkflowRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly TimeProvider _timeProvider;

    public CreateGatePassCommandHandler(
        IGatePassWorkflowRepository repository,
        ICurrentUserService currentUser,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
    }

    public async Task<ApiResponse<GatePassDto>> Handle(
        CreateGatePassCommand command,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.ActiveSchoolId;
        var userId = _currentUser.UserId;
        if (schoolId is null || string.IsNullOrWhiteSpace(userId))
            return ApiResponse<GatePassDto>.Fail(GatePassHandlerSupport.AuthenticationRequired);
        if (!_currentUser.IsInRole(RoleNames.Guardian)
            || !_currentUser.HasPermission(PermissionNames.GatePassRequest))
            return ApiResponse<GatePassDto>.Fail(GatePassHandlerSupport.PermissionDenied);

        var request = command.Request;
        var idempotencyKey = command.IdempotencyKey?.Trim();
        if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > 200)
            return ApiResponse<GatePassDto>.Fail("A valid Idempotency-Key is required");
        if (string.IsNullOrWhiteSpace(request.Reason)
            || string.IsNullOrWhiteSpace(request.PickupPersonName))
            return ApiResponse<GatePassDto>.Fail("Reason and pickup person name are required");

        var now = _timeProvider.GetUtcNow();
        if (request.DesiredExitTime <= now)
            return ApiResponse<GatePassDto>.Fail("Requested exit time must be in the future");
        if (GatePassHandlerSupport.ToTimetableDay(request.DesiredExitTime.DayOfWeek) is null)
            return ApiResponse<GatePassDto>.Fail("Requested exit time must be on a school day");

        var requestedDate = DateOnly.FromDateTime(request.DesiredExitTime.DateTime);
        var link = await _repository.GetGuardianLinkAsync(
            schoolId.Value,
            userId,
            request.StudentId,
            cancellationToken).ConfigureAwait(false);
        if (link is null
            || !link.GuardianIsActive
            || !link.StudentIsActive
            || !link.CanRequestGatePass
            || link.ValidFrom > requestedDate
            || (link.ValidTo is not null && link.ValidTo < requestedDate))
            return ApiResponse<GatePassDto>.Fail("Student is not linked to this guardian");

        var existing = await _repository.GetByIdempotencyKeyAsync(
            schoolId.Value,
            link.GuardianProfileId,
            idempotencyKey,
            cancellationToken).ConfigureAwait(false);
        if (existing is not null)
            return ApiResponse<GatePassDto>.Success(existing, "Gate pass request already exists");

        var enrollment = await _repository.GetActiveEnrollmentAsync(
            schoolId.Value,
            request.StudentId,
            requestedDate,
            cancellationToken).ConfigureAwait(false);
        if (enrollment is null)
            return ApiResponse<GatePassDto>.Fail("Student does not have an active enrollment");

        var hasOverlap = await _repository.HasOverlappingActivePassAsync(
            schoolId.Value,
            request.StudentId,
            request.DesiredExitTime - OverlapTolerance,
            request.DesiredExitTime + OverlapTolerance,
            cancellationToken).ConfigureAwait(false);
        if (hasOverlap)
            return ApiResponse<GatePassDto>.Fail("An overlapping active gate pass already exists");

        var correlationId = Guid.NewGuid();
        var gatePass = new GatePass
        {
            SchoolId = schoolId.Value,
            StudentId = request.StudentId,
            AcademicTermId = enrollment.AcademicTermId,
            RequestedByGuardianProfileId = link.GuardianProfileId,
            IdempotencyKey = idempotencyKey,
            RequestedAt = now,
            RequestedExitAt = request.DesiredExitTime,
            Reason = request.Reason.Trim(),
            PickupPersonName = request.PickupPersonName.Trim(),
            PickupRelationship = request.PickupRelationship?.Trim(),
            PickupIdentityHint = request.PickupIdentityHint?.Trim(),
            CurrentClassroomId = enrollment.ClassroomId,
            Status = GatePassStatus.Requested,
            CreatedAt = now,
            CreatedByUserId = userId,
            UpdatedAt = now,
            UpdatedByUserId = userId
        };
        gatePass.Transitions.Add(GatePassHandlerSupport.Transition(
            gatePass,
            null,
            GatePassStatus.Requested,
            userId,
            RoleNames.Guardian,
            now,
            correlationId,
            request.Reason.Trim()));
        gatePass.AppendDomainEvent(new GatePassRequestedEvent(
            correlationId,
            gatePass.Id,
            gatePass.StudentId,
            gatePass.SchoolId,
            gatePass.AcademicTermId,
            gatePass.RequestedByGuardianProfileId,
            gatePass.RequestedAt,
            gatePass.RequestedExitAt,
            now));

        _repository.Add(gatePass);
        try
        {
            await _repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (GatePassPersistenceConflictException)
        {
            var duplicate = await _repository.GetByIdempotencyKeyAsync(
                schoolId.Value,
                link.GuardianProfileId,
                idempotencyKey,
                cancellationToken).ConfigureAwait(false);
            if (duplicate is not null)
                return ApiResponse<GatePassDto>.Success(duplicate, "Gate pass request already exists");
            throw;
        }
        var dto = await _repository.GetDtoAsync(schoolId.Value, gatePass.Id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("The saved gate pass could not be loaded");
        return ApiResponse<GatePassDto>.Success(dto, "Gate pass requested successfully");
    }
}
