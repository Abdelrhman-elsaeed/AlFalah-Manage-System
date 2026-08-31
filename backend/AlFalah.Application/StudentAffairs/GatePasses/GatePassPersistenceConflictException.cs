namespace AlFalah.Application.StudentAffairs.GatePasses;

public sealed class GatePassPersistenceConflictException : Exception
{
    public GatePassPersistenceConflictException(Exception innerException)
        : base("Gate pass could not be saved because a conflicting record exists", innerException) { }
}
