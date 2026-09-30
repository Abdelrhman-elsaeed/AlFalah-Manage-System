namespace AlFalah.Application.StudentAffairs.Summons;

public sealed class SummonIdempotencyConflictException : Exception
{
    public SummonIdempotencyConflictException(Exception innerException)
        : base("Guardian summons idempotency conflict", innerException) { }
}
