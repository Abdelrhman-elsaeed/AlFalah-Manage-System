using System;

namespace AlFalah.Application.StudentAffairs.Referrals;

public sealed class ReferralConcurrencyException : Exception
{
    public ReferralConcurrencyException(Exception innerException)
        : base("Referral was modified by another transaction", innerException)
    {
    }
}
