namespace AlFalah.Application.StudentAffairs.Summons;

public sealed class SummonConcurrencyException : Exception
{
    public SummonConcurrencyException(Exception innerException)
        : base("Guardian summons concurrency conflict", innerException)
    {
    }
}
