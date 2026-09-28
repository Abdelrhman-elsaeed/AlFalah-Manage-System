namespace AlFalah.Application.StudentAffairs.Referrals;

public sealed class ReferralIdempotencyConflictException : Exception
{
    public ReferralIdempotencyConflictException(Exception innerException)
        : base("Referral idempotency key conflicted with another request", innerException)
    {
    }
}
