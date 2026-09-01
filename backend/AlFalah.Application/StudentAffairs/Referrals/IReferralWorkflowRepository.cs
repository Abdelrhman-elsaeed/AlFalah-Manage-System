using AlFalah.Application.StudentAffairs.DTOs.Referrals;
using AlFalah.Domain.Entities.StudentAffairs;
using AlFalah.Shared.Models;

namespace AlFalah.Application.StudentAffairs.Referrals;

public sealed record ReferralEnrollmentSnapshot(int AcademicTermId, int? ClassroomId, string? ClassLabel);

public interface IReferralWorkflowRepository
{
    Task<PagedResult<ReferralDto>> GetReferralsAsync(
        int schoolId,
        ReferralListQuery query,
        CancellationToken cancellationToken);

    Task<ReferralDto?> GetDtoAsync(
        int schoolId,
        int referralId,
        CancellationToken cancellationToken);

    Task<StudentReferral?> GetForUpdateAsync(
        int schoolId,
        int referralId,
        CancellationToken cancellationToken);

    Task<ReferralEnrollmentSnapshot?> GetActiveEnrollmentAsync(
        int schoolId,
        int studentId,
        DateOnly onDate,
        CancellationToken cancellationToken);

    Task<bool> IsSocialWorkerAsync(
        int schoolId,
        string socialWorkerUserId,
        CancellationToken cancellationToken);

    Task<bool> IsAssignedToAsync(
        int schoolId,
        int referralId,
        string socialWorkerUserId,
        CancellationToken cancellationToken);

    void Add(StudentReferral referral);
    void AddAction(StudentCaseAction action);
    void SetExpectedRowVersion(StudentReferral referral, byte[] rowVersion);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
