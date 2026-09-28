using AlFalah.Application.IntelligentTimetable;
using AlFalah.Application.Interfaces;
using AlFalah.Application.StudentAffairs.DTOs.Permits;
using AlFalah.Domain.Entities.StudentAffairs;
using AlFalah.Domain.Enums;
using AlFalah.Domain.Enums.StudentAffairs;
using AlFalah.Domain.Events;
using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.StudentAffairs.Permits.Handlers;

internal static class ClassroomEntryPermitHandlerSupport
{
    public const string AuthenticationRequired = "An authenticated user and active school are required";
    public const string PermissionDenied = "You do not have permission to perform this classroom entry permit action";
    public const string NotFound = "Classroom entry permit was not found in the caller scope";
    public const string ConcurrencyConflict = "Classroom entry permit was modified by another user";

    public static bool TryDecodeRowVersion(string? encoded, byte[] current, out byte[] expected)
    {
        expected = Array.Empty<byte>();
        if (string.IsNullOrWhiteSpace(encoded)) return false;
        try
        {
            expected = Convert.FromBase64String(encoded);
            return expected.Length > 0 && expected.AsSpan().SequenceEqual(current);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    public static ClassroomEntryPermitViewerScope? ViewerScope(ICurrentUserService currentUser)
    {
        if (!currentUser.HasPermission(PermissionNames.ClassroomEntryPermitView)) return null;
        if (currentUser.IsInRole(RoleNames.StudentAffairsOfficer)) return ClassroomEntryPermitViewerScope.Officer;
        if (currentUser.IsInRole(RoleNames.Instructor)) return ClassroomEntryPermitViewerScope.Instructor;
        if (currentUser.IsInRole(RoleNames.Guardian)) return ClassroomEntryPermitViewerScope.Guardian;
        return null;
    }
}

public sealed class CreateClassroomEntryPermitCommandHandler(
    IClassroomEntryPermitWorkflowRepository repository,
    IBellScheduleRepository schedules,
    ICurrentLessonResolver currentLessonResolver,
    ICurrentUserService currentUser,
    TimeProvider timeProvider)
    : IRequestHandler<CreateClassroomEntryPermitCommand, ApiResponse<ClassroomEntryPermitDto>>
{
    public async Task<ApiResponse<ClassroomEntryPermitDto>> Handle(
        CreateClassroomEntryPermitCommand command,
        CancellationToken cancellationToken)
    {
        var schoolId = currentUser.ActiveSchoolId;
        var userId = currentUser.UserId;
        if (!currentUser.IsAuthenticated || schoolId is null || string.IsNullOrWhiteSpace(userId))
            return ApiResponse<ClassroomEntryPermitDto>.Fail(ClassroomEntryPermitHandlerSupport.AuthenticationRequired);
        if (!currentUser.IsInRole(RoleNames.StudentAffairsOfficer)
            || !currentUser.HasPermission(PermissionNames.ClassroomEntryPermitIssue))
            return ApiResponse<ClassroomEntryPermitDto>.Fail(ClassroomEntryPermitHandlerSupport.PermissionDenied);

        var request = command.Request;
        if (request.StudentId <= 0 || string.IsNullOrWhiteSpace(request.Reason))
            return ApiResponse<ClassroomEntryPermitDto>.Fail("Student and reason are required");
        if (request.ValidUntil <= request.ValidFrom)
            return ApiResponse<ClassroomEntryPermitDto>.Fail("ValidUntil must be after ValidFrom");

        var now = timeProvider.GetUtcNow();
        if (now < request.ValidFrom || now >= request.ValidUntil)
            return ApiResponse<ClassroomEntryPermitDto>.Fail("The server issue instant must be inside the validity window");

        IReadOnlyList<PublishedBellScheduleCandidate> candidates;
        try
        {
            candidates = await schedules.GetPublishedCandidatesAsync(schoolId.Value, now, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (TimeZoneNotFoundException)
        {
            return ApiResponse<ClassroomEntryPermitDto>.Fail("No valid published schedule is available");
        }
        catch (InvalidTimeZoneException)
        {
            return ApiResponse<ClassroomEntryPermitDto>.Fail("No valid published schedule is available");
        }
        if (candidates.Count != 1)
            return ApiResponse<ClassroomEntryPermitDto>.Fail(candidates.Count == 0
                ? "No published schedule is available"
                : "The published schedule is ambiguous");

        var candidate = candidates[0];
        var localDate = DateOnly.FromDateTime(
            BellScheduleResolver.LocalTime(candidate.Schedule, now).DateTime);
        var enrollment = await repository.GetActiveEnrollmentAsync(
            schoolId.Value,
            request.StudentId,
            null,
            candidate.Schedule.AcademicYearId,
            candidate.Schedule.Semester,
            localDate,
            cancellationToken).ConfigureAwait(false);
        if (enrollment is null)
            return ApiResponse<ClassroomEntryPermitDto>.Fail("Student was not found in an active current enrollment");

        var lesson = await currentLessonResolver.ResolveForClassroomAsync(
            schoolId.Value,
            now,
            enrollment.ClassroomId,
            null,
            cancellationToken).ConfigureAwait(false);
        if (lesson.Kind != CurrentLessonResolutionKind.ActiveLesson
            || lesson.Classroom is null
            || lesson.EffectiveInstructor is null
            || lesson.SchoolTimetableId is null
            || lesson.SchoolTimetableEntryId is null
            || lesson.AcademicYearId != enrollment.AcademicYearId
            || lesson.Semester != enrollment.Semester)
            return ApiResponse<ClassroomEntryPermitDto>.Fail(
                $"Classroom entry permit cannot be issued: {lesson.ResolutionReason}");

        var existingId = await repository.FindEquivalentActivePermitIdAsync(
            schoolId.Value,
            request.StudentId,
            lesson.SchoolTimetableEntryId.Value,
            request.ValidFrom,
            request.ValidUntil,
            cancellationToken).ConfigureAwait(false);
        if (existingId is not null)
        {
            var existing = await repository.GetDtoAsync(
                schoolId.Value,
                existingId.Value,
                ClassroomEntryPermitViewerScope.Officer,
                userId,
                now,
                cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException("The existing classroom entry permit could not be loaded");
            return ApiResponse<ClassroomEntryPermitDto>.Success(existing, "An equivalent active permit already exists");
        }

        var permit = new ClassroomEntryPermit
        {
            SchoolId = schoolId.Value,
            StudentId = request.StudentId,
            AcademicTermId = enrollment.AcademicTermId,
            ClassroomId = enrollment.ClassroomId,
            IssuedByStudentAffairsUserId = userId,
            IssuedAt = now,
            Reason = request.Reason.Trim(),
            ValidFrom = request.ValidFrom,
            ValidUntil = request.ValidUntil,
            SchoolTimetableId = lesson.SchoolTimetableId,
            SchoolTimetableEntryId = lesson.SchoolTimetableEntryId,
            TargetInstructorProfileId = lesson.EffectiveInstructor.Id,
            Status = ClassroomEntryPermitStatus.Issued,
            CreatedAt = now,
            CreatedByUserId = userId,
            UpdatedAt = now,
            UpdatedByUserId = userId
        };
        permit.AppendDomainEvent(new ClassroomEntryPermitIssuedEvent(
            Guid.NewGuid(),
            permit.Id,
            permit.StudentId,
            permit.SchoolId,
            permit.AcademicTermId,
            permit.ClassroomId,
            lesson.SchoolTimetableId.Value,
            lesson.SchoolTimetableEntryId.Value,
            lesson.EffectiveInstructor.Id,
            lesson.EffectiveInstructor.UserId,
            now,
            request.ValidUntil,
            now));

        try
        {
            var saveResult = await repository.SaveNewPermitAsync(permit, cancellationToken).ConfigureAwait(false);
            if (!saveResult.Created)
            {
                var existing = await repository.GetDtoAsync(
                    schoolId.Value,
                    saveResult.PermitId,
                    ClassroomEntryPermitViewerScope.Officer,
                    userId,
                    now,
                    cancellationToken).ConfigureAwait(false)
                    ?? throw new InvalidOperationException("The equivalent classroom entry permit could not be loaded");
                return ApiResponse<ClassroomEntryPermitDto>.Success(existing, "An equivalent active permit already exists");
            }
        }
        catch (ClassroomEntryPermitPersistenceConflictException)
        {
            var concurrentId = await repository.FindEquivalentActivePermitIdAsync(
                schoolId.Value,
                request.StudentId,
                lesson.SchoolTimetableEntryId.Value,
                request.ValidFrom,
                request.ValidUntil,
                cancellationToken).ConfigureAwait(false);
            if (concurrentId is null) throw;

            var concurrent = await repository.GetDtoAsync(
                schoolId.Value,
                concurrentId.Value,
                ClassroomEntryPermitViewerScope.Officer,
                userId,
                now,
                cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException("The concurrent classroom entry permit could not be loaded");
            return ApiResponse<ClassroomEntryPermitDto>.Success(
                concurrent,
                "An equivalent active permit was created concurrently");
        }
        var dto = await repository.GetDtoAsync(
            schoolId.Value,
            permit.Id,
            ClassroomEntryPermitViewerScope.Officer,
            userId,
            now,
            cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The saved classroom entry permit could not be loaded");
        return ApiResponse<ClassroomEntryPermitDto>.Success(dto, "Classroom entry permit issued successfully");
    }
}

public sealed class GetClassroomEntryPermitsQueryHandler(
    IClassroomEntryPermitWorkflowRepository repository,
    ICurrentUserService currentUser,
    TimeProvider timeProvider)
    : IRequestHandler<GetClassroomEntryPermitsQuery, ApiResponse<PagedResult<ClassroomEntryPermitDto>>>
{
    public async Task<ApiResponse<PagedResult<ClassroomEntryPermitDto>>> Handle(
        GetClassroomEntryPermitsQuery request,
        CancellationToken cancellationToken)
    {
        var schoolId = currentUser.ActiveSchoolId;
        var userId = currentUser.UserId;
        if (!currentUser.IsAuthenticated || schoolId is null || string.IsNullOrWhiteSpace(userId))
            return ApiResponse<PagedResult<ClassroomEntryPermitDto>>.Fail(ClassroomEntryPermitHandlerSupport.AuthenticationRequired);
        var scope = ClassroomEntryPermitHandlerSupport.ViewerScope(currentUser);
        if (scope is null)
            return ApiResponse<PagedResult<ClassroomEntryPermitDto>>.Fail(ClassroomEntryPermitHandlerSupport.PermissionDenied);

        var result = await repository.GetPermitsAsync(
            schoolId.Value, request.Query, scope.Value, userId, timeProvider.GetUtcNow(), cancellationToken)
            .ConfigureAwait(false);
        return ApiResponse<PagedResult<ClassroomEntryPermitDto>>.Success(result);
    }
}

public sealed class GetClassroomEntryPermitByIdQueryHandler(
    IClassroomEntryPermitWorkflowRepository repository,
    ICurrentUserService currentUser,
    TimeProvider timeProvider)
    : IRequestHandler<GetClassroomEntryPermitByIdQuery, ApiResponse<ClassroomEntryPermitDto>>
{
    public async Task<ApiResponse<ClassroomEntryPermitDto>> Handle(
        GetClassroomEntryPermitByIdQuery request,
        CancellationToken cancellationToken)
    {
        var schoolId = currentUser.ActiveSchoolId;
        var userId = currentUser.UserId;
        if (!currentUser.IsAuthenticated || schoolId is null || string.IsNullOrWhiteSpace(userId))
            return ApiResponse<ClassroomEntryPermitDto>.Fail(ClassroomEntryPermitHandlerSupport.AuthenticationRequired);
        var scope = ClassroomEntryPermitHandlerSupport.ViewerScope(currentUser);
        if (scope is null)
            return ApiResponse<ClassroomEntryPermitDto>.Fail(ClassroomEntryPermitHandlerSupport.PermissionDenied);
        var dto = await repository.GetDtoAsync(
            schoolId.Value, request.PermitId, scope.Value, userId, timeProvider.GetUtcNow(), cancellationToken)
            .ConfigureAwait(false);
        return dto is null
            ? ApiResponse<ClassroomEntryPermitDto>.Fail(ClassroomEntryPermitHandlerSupport.NotFound)
            : ApiResponse<ClassroomEntryPermitDto>.Success(dto);
    }
}

public sealed class AcknowledgeClassroomEntryPermitCommandHandler(
    IClassroomEntryPermitWorkflowRepository repository,
    ICurrentUserService currentUser,
    TimeProvider timeProvider)
    : IRequestHandler<AcknowledgeClassroomEntryPermitCommand, ApiResponse<ClassroomEntryPermitDto>>
{
    public async Task<ApiResponse<ClassroomEntryPermitDto>> Handle(
        AcknowledgeClassroomEntryPermitCommand command,
        CancellationToken cancellationToken)
    {
        var schoolId = currentUser.ActiveSchoolId;
        var userId = currentUser.UserId;
        if (!currentUser.IsAuthenticated || schoolId is null || string.IsNullOrWhiteSpace(userId))
            return ApiResponse<ClassroomEntryPermitDto>.Fail(ClassroomEntryPermitHandlerSupport.AuthenticationRequired);
        if (!currentUser.IsInRole(RoleNames.Instructor)
            || !currentUser.HasPermission(PermissionNames.ClassroomEntryPermitAcknowledge))
            return ApiResponse<ClassroomEntryPermitDto>.Fail(ClassroomEntryPermitHandlerSupport.PermissionDenied);

        var instructorProfileId = await repository.GetInstructorProfileIdAsync(
            schoolId.Value, userId, cancellationToken).ConfigureAwait(false);
        var permit = await repository.GetForUpdateAsync(
            schoolId.Value, command.PermitId, cancellationToken).ConfigureAwait(false);
        if (permit is null || instructorProfileId is null || permit.TargetInstructorProfileId != instructorProfileId)
            return ApiResponse<ClassroomEntryPermitDto>.Fail(ClassroomEntryPermitHandlerSupport.NotFound);
        if (!ClassroomEntryPermitHandlerSupport.TryDecodeRowVersion(
                command.Request.RowVersion, permit.RowVersion, out var expected))
            return ApiResponse<ClassroomEntryPermitDto>.Fail(ClassroomEntryPermitHandlerSupport.ConcurrencyConflict);

        var now = timeProvider.GetUtcNow();
        if (permit.Status == ClassroomEntryPermitStatus.AcknowledgedByTeacher
            && permit.AcknowledgedByTeacherUserId == userId)
        {
            var existing = await repository.GetDtoAsync(
                schoolId.Value, permit.Id, ClassroomEntryPermitViewerScope.Instructor, userId, now, cancellationToken)
                .ConfigureAwait(false)
                ?? throw new InvalidOperationException("The acknowledged classroom entry permit could not be loaded");
            return ApiResponse<ClassroomEntryPermitDto>.Success(existing, "Classroom entry permit was already acknowledged");
        }
        if (permit.Status != ClassroomEntryPermitStatus.Issued || now >= permit.ValidUntil)
            return ApiResponse<ClassroomEntryPermitDto>.Fail("Classroom entry permit is expired, revoked, or cannot be acknowledged");

        repository.SetExpectedRowVersion(permit, expected);
        permit.Status = ClassroomEntryPermitStatus.AcknowledgedByTeacher;
        permit.AcknowledgedByTeacherUserId = userId;
        permit.AcknowledgedAt = now;
        permit.UpdatedAt = now;
        permit.UpdatedByUserId = userId;
        try
        {
            await repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (ClassroomEntryPermitConcurrencyException)
        {
            return ApiResponse<ClassroomEntryPermitDto>.Fail(ClassroomEntryPermitHandlerSupport.ConcurrencyConflict);
        }

        var dto = await repository.GetDtoAsync(
            schoolId.Value, permit.Id, ClassroomEntryPermitViewerScope.Instructor, userId, now, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("The acknowledged classroom entry permit could not be loaded");
        return ApiResponse<ClassroomEntryPermitDto>.Success(dto, "Classroom entry permit acknowledged");
    }
}

public sealed class RevokeClassroomEntryPermitCommandHandler(
    IClassroomEntryPermitWorkflowRepository repository,
    ICurrentUserService currentUser,
    TimeProvider timeProvider)
    : IRequestHandler<RevokeClassroomEntryPermitCommand, ApiResponse<ClassroomEntryPermitDto>>
{
    public async Task<ApiResponse<ClassroomEntryPermitDto>> Handle(
        RevokeClassroomEntryPermitCommand command,
        CancellationToken cancellationToken)
    {
        var schoolId = currentUser.ActiveSchoolId;
        var userId = currentUser.UserId;
        if (!currentUser.IsAuthenticated || schoolId is null || string.IsNullOrWhiteSpace(userId))
            return ApiResponse<ClassroomEntryPermitDto>.Fail(ClassroomEntryPermitHandlerSupport.AuthenticationRequired);
        if (!currentUser.IsInRole(RoleNames.StudentAffairsOfficer)
            || !currentUser.HasPermission(PermissionNames.ClassroomEntryPermitRevoke))
            return ApiResponse<ClassroomEntryPermitDto>.Fail(ClassroomEntryPermitHandlerSupport.PermissionDenied);
        if (string.IsNullOrWhiteSpace(command.Request.Reason))
            return ApiResponse<ClassroomEntryPermitDto>.Fail("Revocation reason is required");

        var permit = await repository.GetForUpdateAsync(
            schoolId.Value, command.PermitId, cancellationToken).ConfigureAwait(false);
        if (permit is null)
            return ApiResponse<ClassroomEntryPermitDto>.Fail(ClassroomEntryPermitHandlerSupport.NotFound);
        if (!ClassroomEntryPermitHandlerSupport.TryDecodeRowVersion(
                command.Request.RowVersion, permit.RowVersion, out var expected))
            return ApiResponse<ClassroomEntryPermitDto>.Fail(ClassroomEntryPermitHandlerSupport.ConcurrencyConflict);

        var now = timeProvider.GetUtcNow();
        if (permit.Status == ClassroomEntryPermitStatus.Revoked
            || permit.Status == ClassroomEntryPermitStatus.Expired
            || now >= permit.ValidUntil)
            return ApiResponse<ClassroomEntryPermitDto>.Fail("Classroom entry permit is already revoked or expired");

        repository.SetExpectedRowVersion(permit, expected);
        permit.Status = ClassroomEntryPermitStatus.Revoked;
        permit.RevokedByUserId = userId;
        permit.RevokedAt = now;
        permit.RevocationReason = command.Request.Reason.Trim();
        permit.UpdatedAt = now;
        permit.UpdatedByUserId = userId;
        try
        {
            await repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (ClassroomEntryPermitConcurrencyException)
        {
            return ApiResponse<ClassroomEntryPermitDto>.Fail(ClassroomEntryPermitHandlerSupport.ConcurrencyConflict);
        }

        var dto = await repository.GetDtoAsync(
            schoolId.Value, permit.Id, ClassroomEntryPermitViewerScope.Officer, userId, now, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("The revoked classroom entry permit could not be loaded");
        return ApiResponse<ClassroomEntryPermitDto>.Success(dto, "Classroom entry permit revoked");
    }
}
