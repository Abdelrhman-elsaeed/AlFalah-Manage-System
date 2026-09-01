using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AlFalah.Application.StudentAffairs.DTOs.Shared;
using AlFalah.Application.StudentAffairs.DTOs.Summons;
using AlFalah.Application.StudentAffairs.Summons;
using AlFalah.Domain.Entities.StudentAffairs;
using AlFalah.Domain.Enums;
using AlFalah.Domain.Enums.StudentAffairs;
using AlFalah.Infrastructure.Data;
using AlFalah.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace AlFalah.Infrastructure.Repositories;

public sealed class SummonWorkflowRepository : ISummonWorkflowRepository
{
    private readonly AlFalahDbContext _context;

    public SummonWorkflowRepository(AlFalahDbContext context) => _context = context;

    public async Task<PagedResult<SummonDto>> GetSummonsAsync(
        int schoolId,
        SummonListQuery query,
        CancellationToken cancellationToken)
    {
        var page = query.PageNumber <= 0 ? 1 : query.PageNumber;
        var pageSize = query.PageSize <= 0 ? 20 : query.PageSize;

        var dbQuery = _context.GuardianSummons
            .AsNoTracking()
            .Where(summon => summon.SchoolId == schoolId && !summon.IsDeleted);

        if (query.Status.HasValue)
        {
            dbQuery = dbQuery.Where(summon => summon.Status == query.Status.Value);
        }

        if (query.Priority.HasValue)
        {
            dbQuery = dbQuery.Where(summon => summon.Priority == query.Priority.Value);
        }

        if (query.StudentId.HasValue)
        {
            dbQuery = dbQuery.Where(summon => summon.StudentId == query.StudentId.Value);
        }

        if (query.AppointmentDate.HasValue)
        {
            var date = query.AppointmentDate.Value;
            var startUtc = new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            var endUtc = new DateTimeOffset(date.ToDateTime(TimeOnly.MaxValue), TimeSpan.Zero);
            dbQuery = dbQuery.Where(summon => summon.ScheduledAt.HasValue
                && summon.ScheduledAt.Value >= startUtc
                && summon.ScheduledAt.Value <= endUtc);
        }

        if (!string.IsNullOrWhiteSpace(query.AssignedWorkerUserId))
        {
            dbQuery = dbQuery.Where(summon =>
                summon.ScheduledBySocialWorkerUserId == query.AssignedWorkerUserId
                || (summon.StudentReferral != null && summon.StudentReferral.AssignedSocialWorkerUserId == query.AssignedWorkerUserId));
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            dbQuery = dbQuery.Where(summon =>
                summon.Student.FirstName.Contains(term)
                || (summon.Student.MiddleName != null && summon.Student.MiddleName.Contains(term))
                || summon.Student.LastName.Contains(term)
                || summon.Student.StudentNumber.Contains(term)
                || summon.CreatedReason.Contains(term)
                || (summon.Location != null && summon.Location.Contains(term)));
        }

        var totalCount = await dbQuery.CountAsync(cancellationToken).ConfigureAwait(false);

        var projections = await dbQuery
            .OrderByDescending(summon => summon.ScheduledAt ?? summon.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
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
                    ? summon.ScheduledBySocialWorkerUserId
                    : summon.StudentReferral.AssignedSocialWorkerUserId,
                AssignedWorkerFirstName = summon.StudentReferral == null || summon.StudentReferral.AssignedSocialWorkerUser == null
                    ? null
                    : summon.StudentReferral.AssignedSocialWorkerUser.FirstName,
                AssignedWorkerLastName = summon.StudentReferral == null || summon.StudentReferral.AssignedSocialWorkerUser == null
                    ? null
                    : summon.StudentReferral.AssignedSocialWorkerUser.LastName,
                summon.RequiresOfficerReview,
                summon.OfficerReviewReason,
                summon.GuardianNotifiedAt,
                summon.RowVersion
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var items = projections.Select(row =>
        {
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
        }).ToList();

        return new PagedResult<SummonDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<PagedResult<SummonDto>> GetMySummonsAsync(
        int schoolId,
        string guardianUserId,
        SummonListQuery query,
        CancellationToken cancellationToken)
    {
        var page = query.PageNumber <= 0 ? 1 : query.PageNumber;
        var pageSize = query.PageSize <= 0 ? 20 : query.PageSize;

        var dbQuery = _context.GuardianSummons
            .AsNoTracking()
            .Where(summon => summon.SchoolId == schoolId
                && !summon.IsDeleted
                && summon.GuardianProfile.SchoolId == schoolId
                && summon.GuardianProfile.ApplicationUserId == guardianUserId);

        if (query.Status.HasValue)
        {
            dbQuery = dbQuery.Where(summon => summon.Status == query.Status.Value);
        }

        if (query.Priority.HasValue)
        {
            dbQuery = dbQuery.Where(summon => summon.Priority == query.Priority.Value);
        }

        if (query.StudentId.HasValue)
        {
            dbQuery = dbQuery.Where(summon => summon.StudentId == query.StudentId.Value);
        }

        if (query.AppointmentDate.HasValue)
        {
            var date = query.AppointmentDate.Value;
            var startUtc = new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            var endUtc = new DateTimeOffset(date.ToDateTime(TimeOnly.MaxValue), TimeSpan.Zero);
            dbQuery = dbQuery.Where(summon => summon.ScheduledAt.HasValue
                && summon.ScheduledAt.Value >= startUtc
                && summon.ScheduledAt.Value <= endUtc);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            dbQuery = dbQuery.Where(summon =>
                summon.Student.FirstName.Contains(term)
                || (summon.Student.MiddleName != null && summon.Student.MiddleName.Contains(term))
                || summon.Student.LastName.Contains(term)
                || summon.Student.StudentNumber.Contains(term)
                || summon.CreatedReason.Contains(term)
                || (summon.Location != null && summon.Location.Contains(term)));
        }

        var totalCount = await dbQuery.CountAsync(cancellationToken).ConfigureAwait(false);

        var projections = await dbQuery
            .OrderByDescending(summon => summon.ScheduledAt ?? summon.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
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
                    ? summon.ScheduledBySocialWorkerUserId
                    : summon.StudentReferral.AssignedSocialWorkerUserId,
                AssignedWorkerFirstName = summon.StudentReferral == null || summon.StudentReferral.AssignedSocialWorkerUser == null
                    ? null
                    : summon.StudentReferral.AssignedSocialWorkerUser.FirstName,
                AssignedWorkerLastName = summon.StudentReferral == null || summon.StudentReferral.AssignedSocialWorkerUser == null
                    ? null
                    : summon.StudentReferral.AssignedSocialWorkerUser.LastName,
                summon.RequiresOfficerReview,
                summon.OfficerReviewReason,
                summon.GuardianNotifiedAt,
                summon.RowVersion
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var items = projections.Select(row =>
        {
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
        }).ToList();

        return new PagedResult<SummonDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<SummonHistoryDto?> GetHistoryAsync(
        int schoolId,
        int summonId,
        CancellationToken cancellationToken)
    {
        var exists = await _context.GuardianSummons
            .AsNoTracking()
            .AnyAsync(s => s.Id == summonId && s.SchoolId == schoolId && !s.IsDeleted, cancellationToken)
            .ConfigureAwait(false);

        if (!exists) return null;

        var transitions = await _context.GuardianSummonStatusHistories
            .AsNoTracking()
            .Where(t => t.SchoolId == schoolId && t.GuardianSummonId == summonId)
            .OrderBy(t => t.OccurredAt)
            .Select(t => new
            {
                FromStatus = t.FromStatus.HasValue ? t.FromStatus.Value.ToString() : null,
                ToStatus = t.ToStatus.ToString(),
                t.ActorUserId,
                ActorFirstName = t.ActorUser.FirstName,
                ActorLastName = t.ActorUser.LastName,
                t.OccurredAt,
                t.Notes
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var transitionDtos = transitions.Select(t => new TransitionDto(
            t.FromStatus,
            t.ToStatus,
            new ActorSummaryDto(
                t.ActorUserId,
                $"{t.ActorFirstName} {t.ActorLastName}".Trim(),
                RoleNames.SocialWorker),
            t.OccurredAt,
            t.Notes)).ToList();

        return new SummonHistoryDto(transitionDtos);
    }

    public Task<GuardianSummon?> GetForUpdateAsync(
        int schoolId,
        int summonId,
        CancellationToken cancellationToken) =>
        _context.GuardianSummons
            .AsTracking()
            .Where(summon => summon.Id == summonId && summon.SchoolId == schoolId && !summon.IsDeleted)
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
            && !summon.IsDeleted
            && (summon.StudentReferralId == null
                || summon.ScheduledBySocialWorkerUserId == socialWorkerUserId
                || (summon.StudentReferral!.SchoolId == schoolId
                    && (summon.StudentReferral.AssignedSocialWorkerUserId == null
                        || summon.StudentReferral.AssignedSocialWorkerUserId == socialWorkerUserId))),
            cancellationToken);

    public Task<SummonEnrollmentSnapshot?> GetActiveEnrollmentAsync(
        int schoolId,
        int studentId,
        DateOnly onDate,
        CancellationToken cancellationToken) =>
        _context.StudentEnrollments
            .AsNoTracking()
            .Where(e => e.SchoolId == schoolId
                && e.StudentId == studentId
                && e.Student.IsActive
                && e.Status == StudentEnrollmentStatus.Active
                && e.EnrolledOn <= onDate
                && (e.WithdrawnOn == null || e.WithdrawnOn >= onDate)
                && e.AcademicTerm.SchoolId == schoolId
                && e.AcademicTerm.IsActive
                && e.AcademicTerm.StartsOn <= onDate
                && e.AcademicTerm.EndsOn >= onDate)
            .Select(e => new SummonEnrollmentSnapshot(e.AcademicTermId))
            .FirstOrDefaultAsync(cancellationToken);

    public void Add(GuardianSummon summon) => _context.GuardianSummons.Add(summon);

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
            .Where(summon => summon.Id == summonId && summon.SchoolId == schoolId && !summon.IsDeleted)
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
                    ? summon.ScheduledBySocialWorkerUserId
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
