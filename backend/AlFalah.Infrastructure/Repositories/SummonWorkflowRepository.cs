using AlFalah.Application.StudentAffairs.DTOs.Shared;
using AlFalah.Application.StudentAffairs.DTOs.Summons;
using AlFalah.Application.StudentAffairs.Summons;
using AlFalah.Domain.Entities.StudentAffairs;
using AlFalah.Domain.Enums;
using AlFalah.Domain.Enums.StudentAffairs;
using AlFalah.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AlFalah.Infrastructure.Repositories;

public sealed class SummonWorkflowRepository : ISummonWorkflowRepository
{
    private readonly AlFalahDbContext _context;

    public SummonWorkflowRepository(AlFalahDbContext context) => _context = context;

    public Task<GuardianSummon?> GetForUpdateAsync(
        int schoolId,
        int summonId,
        CancellationToken cancellationToken) =>
        _context.GuardianSummons
            .AsTracking()
            .Where(summon => summon.Id == summonId && summon.SchoolId == schoolId)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<bool> IsGuardianLinkActiveAsync(
        int schoolId,
        int guardianProfileId,
        int studentId,
        DateOnly onDate,
        CancellationToken cancellationToken) =>
        _context.StudentGuardians.AsNoTracking().AnyAsync(link =>
            link.SchoolId == schoolId
            && link.GuardianProfileId == guardianProfileId
            && link.StudentId == studentId
            && link.GuardianProfile.SchoolId == schoolId
            && link.GuardianProfile.IsActive
            && link.Student.SchoolId == schoolId
            && link.Student.IsActive
            && link.ValidFrom <= onDate
            && (link.ValidTo == null || link.ValidTo >= onDate),
            cancellationToken);

    public Task<bool> IsAssignedToAsync(
        int schoolId,
        int summonId,
        string socialWorkerUserId,
        CancellationToken cancellationToken) =>
        _context.GuardianSummons.AsNoTracking().AnyAsync(summon =>
            summon.Id == summonId
            && summon.SchoolId == schoolId
            && (summon.StudentReferralId == null
                || (summon.StudentReferral!.SchoolId == schoolId
                    && summon.StudentReferral.AssignedSocialWorkerUserId == socialWorkerUserId)),
            cancellationToken);

    public void SetExpectedRowVersion(GuardianSummon summon, byte[] rowVersion) =>
        _context.Entry(summon).Property(entity => entity.RowVersion).OriginalValue = rowVersion;

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new SummonConcurrencyException(exception);
        }
    }

    public async Task<SummonDto?> GetDtoAsync(
        int schoolId,
        int summonId,
        CancellationToken cancellationToken)
    {
        var row = await _context.GuardianSummons
            .AsNoTracking()
            .Where(summon => summon.Id == summonId && summon.SchoolId == schoolId)
            .Select(summon => new
            {
                summon.Id,
                summon.StudentId,
                summon.Student.StudentNumber,
                StudentName = (summon.Student.FirstName + " "
                    + (summon.Student.MiddleName ?? string.Empty) + " "
                    + summon.Student.LastName).Trim(),
                summon.Student.IsActive,
                Enrollment = summon.Student.Enrollments
                    .Where(enrollment => enrollment.SchoolId == schoolId
                        && enrollment.AcademicTermId == summon.AcademicTermId
                        && enrollment.Status == StudentEnrollmentStatus.Active)
                    .Select(enrollment => new
                    {
                        enrollment.ClassroomId,
                        enrollment.Classroom.ClassLabel
                    })
                    .FirstOrDefault(),
                summon.StudentReferralId,
                summon.CreatedReason,
                summon.Priority,
                summon.SourceCountSnapshot,
                summon.ThresholdSnapshot,
                summon.Status,
                summon.ScheduledAt,
                summon.Location,
                summon.Instructions,
                summon.GuardianProfileId,
                GuardianFirstName = summon.GuardianProfile.ApplicationUser.FirstName,
                GuardianLastName = summon.GuardianProfile.ApplicationUser.LastName,
                GuardianLink = summon.Student.Guardians
                    .Where(link => link.SchoolId == schoolId
                        && link.GuardianProfileId == summon.GuardianProfileId)
                    .Select(link => new
                    {
                        link.RelationshipType,
                        link.IsPrimary,
                        link.ReceivesNotifications
                    })
                    .FirstOrDefault(),
                AssignedWorkerUserId = summon.StudentReferral == null
                    ? null
                    : summon.StudentReferral.AssignedSocialWorkerUserId,
                AssignedWorkerFirstName = summon.StudentReferral == null
                    || summon.StudentReferral.AssignedSocialWorkerUser == null
                        ? null
                        : summon.StudentReferral.AssignedSocialWorkerUser.FirstName,
                AssignedWorkerLastName = summon.StudentReferral == null
                    || summon.StudentReferral.AssignedSocialWorkerUser == null
                        ? null
                        : summon.StudentReferral.AssignedSocialWorkerUser.LastName,
                summon.RequiresOfficerReview,
                summon.OfficerReviewReason,
                summon.GuardianNotifiedAt,
                summon.RowVersion
            })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (row is null) return null;

        var student = new StudentSummaryDto(
            row.StudentId,
            row.StudentNumber,
            row.StudentName,
            row.Enrollment?.ClassroomId,
            row.Enrollment?.ClassLabel,
            row.IsActive,
            null);
        var guardian = new GuardianSummaryDto(
            row.GuardianProfileId,
            $"{row.GuardianFirstName} {row.GuardianLastName}".Trim(),
            row.GuardianLink?.RelationshipType ?? GuardianRelationshipType.Other,
            row.GuardianLink?.IsPrimary ?? false,
            row.GuardianLink?.ReceivesNotifications ?? false);
        var assignedWorker = row.AssignedWorkerUserId is null
            ? null
            : new ActorSummaryDto(
                row.AssignedWorkerUserId,
                $"{row.AssignedWorkerFirstName} {row.AssignedWorkerLastName}".Trim(),
                RoleNames.SocialWorker);

        return new SummonDto(
            row.Id,
            student,
            row.StudentReferralId,
            row.CreatedReason,
            row.Priority,
            row.SourceCountSnapshot,
            row.ThresholdSnapshot,
            row.Status,
            row.ScheduledAt,
            row.Location,
            row.Instructions,
            guardian,
            assignedWorker,
            row.RequiresOfficerReview,
            row.OfficerReviewReason,
            row.GuardianNotifiedAt,
            Convert.ToBase64String(row.RowVersion));
    }
}
