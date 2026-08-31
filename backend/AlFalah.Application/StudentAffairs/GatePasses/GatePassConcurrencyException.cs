namespace AlFalah.Application.StudentAffairs.GatePasses;

public sealed class GatePassConcurrencyException : Exception
{
    public GatePassConcurrencyException(Exception innerException)
        : base("Gate pass was modified by another user", innerException) { }
}
