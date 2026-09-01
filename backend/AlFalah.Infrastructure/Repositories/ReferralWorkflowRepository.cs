using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AlFalah.Application.StudentAffairs.DTOs.Referrals;
using AlFalah.Application.StudentAffairs.DTOs.Shared;
using AlFalah.Application.StudentAffairs.Referrals;
using AlFalah.Domain.Entities.StudentAffairs;
using AlFalah.Domain.Enums;
using AlFalah.Domain.Enums.StudentAffairs;
using AlFalah.Infrastructure.Data;
using AlFalah.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace AlFalah.Infrastructure.Repositories;

public sealed class ReferralWorkflowRepository : IReferralWorkflowRepository
{
    private readonly AlFalahDbContext _context;

    public ReferralWorkflowRepository(AlFalahDbContext context) => _context = context;

    public async Task<PagedResult<ReferralDto>> GetReferralsAsync(
        int schoolId,
        ReferralListQuery query,
        CancellationToken cancellationToken)
    {
        var page = query.PageNumber <= 0 ? 1 : query.PageNumber;
        var pageSize = query.PageSize <= 0 ? 20 : query.PageSize;

        var dbQuery = _context.StudentReferrals
            .AsNoTracking()
            .Where(r => r.SchoolId == schoolId && !r.IsDeleted);

        if (query.Status.HasValue)
        {
            dbQuery = dbQuery.Where(r => r.Status == query.Status.Value);
        }

        if (query.Priority.HasValue)
        {
            dbQuery = dbQuery.Where(r => r.Priority == query.Priority.Value);
        }

        if (query.StudentId.HasValue)
        {
            dbQuery = dbQuery.Where(r => r.StudentId == query.StudentId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.AssignedWorkerUserId))
        {
            dbQuery = dbQuery.Where(r => r.AssignedSocialWorkerUserId == query.AssignedWorkerUserId);
        }

        if (query.IsAssigned.HasValue)
        {
            dbQuery = query.IsAssigned.Value
                ? dbQuery.Where(r => r.AssignedSocialWorkerUserId != null)
                : dbQuery.Where(r => r.AssignedSocialWorkerUserId == null);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            dbQuery = dbQuery.Where(r =>
                r.Student.FirstName.Contains(term)
                || (r.Student.MiddleName != null && r.Student.MiddleName.Contains(term))
                || r.Student.LastName.Contains(term)
                || r.Student.StudentNumber.Contains(term)
                || (r.RecommendedActions != null && r.RecommendedActions.Contains(term))
                || (r.ResolutionNotes != null && r.ResolutionNotes.Contains(term)));
        }

        var totalCount = await dbQuery.CountAsync(cancellationToken).ConfigureAwait(false);

        var projections = await dbQuery
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new
            {
                r.Id,
                r.StudentId,
                r.Student.StudentNumber,
                StudentDisplayName = (r.Student.FirstName + " "
                    + (r.Student.MiddleName ?? string.Empty) + " "
                    + r.Student.LastName).Trim(),
                r.Student.IsActive,
                Enrollment = r.Student.Enrollments
                    .Where(e => e.SchoolId == schoolId
                        && e.AcademicTermId == r.AcademicTermId
                        && e.Status == StudentEnrollmentStatus.Active)
                    .Select(e => new
                    {
                        e.ClassroomId,
                        e.Classroom.ClassLabel
                    })
                    .FirstOrDefault(),
                r.SourceType,
                r.SourceEntityId,
                r.CountSnapshot,
                r.ThresholdSnapshot,
                r.Priority,
                r.Status,
                r.AssignedSocialWorkerUserId,
                AssignedWorkerFirstName = r.AssignedSocialWorkerUser == null ? null : r.AssignedSocialWorkerUser.FirstName,
                AssignedWorkerLastName = r.AssignedSocialWorkerUser == null ? null : r.AssignedSocialWorkerUser.LastName,
                Actions = r.Actions
                    .Where(a => !a.IsDeleted)
                    .OrderBy(a => a.ActionAt)
                    .Select(a => new
                    {
                        a.Id,
                        a.ActionType,
                        a.Description,
                        a.ActorUserId,
                        ActorFirstName = a.ActorUser.FirstName,
                        ActorLastName = a.ActorUser.LastName,
                        a.ActionAt,
                        a.Result
                    })
                    .ToList(),
                r.ResolutionNotes,
                r.CreatedAt,
                r.RowVersion
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var items = projections.Select(p =>
        {
            var student = new StudentSummaryDto(
                p.StudentId,
                p.StudentNumber,
                p.StudentDisplayName,
                p.Enrollment?.ClassroomId,
                p.Enrollment?.ClassLabel,
                p.IsActive,
                null);

            var sourceSnapshot = new ReferralSourceSnapshotDto(
                p.SourceType,
                p.SourceEntityId,
                p.CountSnapshot,
                p.ThresholdSnapshot);

            var assignedWorker = p.AssignedSocialWorkerUserId == null
                ? null
                : new ActorSummaryDto(
                    p.AssignedSocialWorkerUserId,
                    $"{p.AssignedWorkerFirstName} {p.AssignedWorkerLastName}".Trim(),
                    RoleNames.SocialWorker);

            var actions = p.Actions.Select(a => new StudentCaseActionDto(
                a.Id,
                a.ActionType,
                a.Description,
                new ActorSummaryDto(
                    a.ActorUserId,
                    $"{a.ActorFirstName} {a.ActorLastName}".Trim(),
                    RoleNames.SocialWorker),
                a.ActionAt,
                a.Result)).ToList();

            return new ReferralDto(
                p.Id,
                student,
                sourceSnapshot,
                null,
                p.Priority,
                p.Status,
                assignedWorker,
                actions,
                p.ResolutionNotes,
                p.CreatedAt,
                Convert.ToBase64String(p.RowVersion));
        }).ToList();

        return new PagedResult<ReferralDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<ReferralDto?> GetDtoAsync(
        int schoolId,
        int referralId,
        CancellationToken cancellationToken)
    {
        var row = await _context.StudentReferrals
            .AsNoTracking()
            .Where(r => r.Id == referralId && r.SchoolId == schoolId && !r.IsDeleted)
            .Select(r => new
            {
                r.Id,
                r.StudentId,
                r.Student.StudentNumber,
                StudentDisplayName = (r.Student.FirstName + " "
                    + (r.Student.MiddleName ?? string.Empty) + " "
                    + r.Student.LastName).Trim(),
                r.Student.IsActive,
                Enrollment = r.Student.Enrollments
                    .Where(e => e.SchoolId == schoolId
                        && e.AcademicTermId == r.AcademicTermId
                        && e.Status == StudentEnrollmentStatus.Active)
                    .Select(e => new
                    {
                        e.ClassroomId,
                        e.Classroom.ClassLabel
                    })
                    .FirstOrDefault(),
                r.SourceType,
                r.SourceEntityId,
                r.CountSnapshot,
                r.ThresholdSnapshot,
                r.Priority,
                r.Status,
                r.AssignedSocialWorkerUserId,
                AssignedWorkerFirstName = r.AssignedSocialWorkerUser == null ? null : r.AssignedSocialWorkerUser.FirstName,
                AssignedWorkerLastName = r.AssignedSocialWorkerUser == null ? null : r.AssignedSocialWorkerUser.LastName,
                Actions = r.Actions
                    .Where(a => !a.IsDeleted)
                    .OrderBy(a => a.ActionAt)
                    .Select(a => new
                    {
                        a.Id,
                        a.ActionType,
                        a.Description,
                        a.ActorUserId,
                        ActorFirstName = a.ActorUser.FirstName,
                        ActorLastName = a.ActorUser.LastName,
                        a.ActionAt,
                        a.Result
                    })
                    .ToList(),
                r.ResolutionNotes,
                r.CreatedAt,
                r.RowVersion
            })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (row is null) return null;

        var student = new StudentSummaryDto(
            row.StudentId,
            row.StudentNumber,
            row.StudentDisplayName,
            row.Enrollment?.ClassroomId,
            row.Enrollment?.ClassLabel,
            row.IsActive,
            null);

        var sourceSnapshot = new ReferralSourceSnapshotDto(
            row.SourceType,
            row.SourceEntityId,
            row.CountSnapshot,
            row.ThresholdSnapshot);

        var assignedWorker = row.AssignedSocialWorkerUserId == null
            ? null
            : new ActorSummaryDto(
                row.AssignedSocialWorkerUserId,
                $"{row.AssignedWorkerFirstName} {row.AssignedWorkerLastName}".Trim(),
                RoleNames.SocialWorker);

        var actions = row.Actions.Select(a => new StudentCaseActionDto(
            a.Id,
            a.ActionType,
            a.Description,
            new ActorSummaryDto(
                a.ActorUserId,
                $"{a.ActorFirstName} {a.ActorLastName}".Trim(),
                RoleNames.SocialWorker),
            a.ActionAt,
            a.Result)).ToList();

        return new ReferralDto(
            row.Id,
            student,
            sourceSnapshot,
            null,
            row.Priority,
            row.Status,
            assignedWorker,
            actions,
            row.ResolutionNotes,
            row.CreatedAt,
            Convert.ToBase64String(row.RowVersion));
    }

    public Task<StudentReferral?> GetForUpdateAsync(
        int schoolId,
        int referralId,
        CancellationToken cancellationToken) =>
        _context.StudentReferrals
            .AsTracking()
            .Where(r => r.Id == referralId && r.SchoolId == schoolId && !r.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<ReferralEnrollmentSnapshot?> GetActiveEnrollmentAsync(
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
            .Select(e => new ReferralEnrollmentSnapshot(
                e.AcademicTermId,
                e.ClassroomId,
                e.Classroom.ClassLabel))
            .FirstOrDefaultAsync(cancellationToken);

    public Task<bool> IsSocialWorkerAsync(
        int schoolId,
        string socialWorkerUserId,
        CancellationToken cancellationToken) =>
        _context.Users
            .AsNoTracking()
            .AnyAsync(u => u.Id == socialWorkerUserId
                && u.IsActive
                && (_context.UserSchoolRoles.Any(usr => usr.SchoolId == schoolId
                    && usr.UserId == socialWorkerUserId
                    && usr.IsActive
                    && usr.Role.Name == RoleNames.SocialWorker)
                    || _context.UserRoles.Any(ur => ur.UserId == socialWorkerUserId
                        && _context.Roles.Any(r => r.Id == ur.RoleId && r.Name == RoleNames.SocialWorker))),
                cancellationToken);

    public Task<bool> IsAssignedToAsync(
        int schoolId,
        int referralId,
        string socialWorkerUserId,
        CancellationToken cancellationToken) =>
        _context.StudentReferrals
            .AsNoTracking()
            .AnyAsync(r => r.Id == referralId
                && r.SchoolId == schoolId
                && !r.IsDeleted
                && (r.AssignedSocialWorkerUserId == null || r.AssignedSocialWorkerUserId == socialWorkerUserId),
                cancellationToken);

    public void Add(StudentReferral referral) => _context.StudentReferrals.Add(referral);

    public void AddAction(StudentCaseAction action) => _context.StudentCaseActions.Add(action);

    public void SetExpectedRowVersion(StudentReferral referral, byte[] rowVersion) =>
        _context.Entry(referral).Property(e => e.RowVersion).OriginalValue = rowVersion;

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new ReferralConcurrencyException(exception);
        }
    }
}
