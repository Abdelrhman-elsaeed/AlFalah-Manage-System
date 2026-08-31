namespace AlFalah.Application.StudentAffairs.Attendance;

public sealed class AttendanceConcurrencyException : Exception
{
    public AttendanceConcurrencyException(Exception innerException)
        : base("The attendance workflow was modified concurrently", innerException) { }
}

public sealed class AttendancePersistenceConflictException : Exception
{
    public AttendancePersistenceConflictException(Exception innerException)
        : base("The attendance workflow could not be persisted", innerException) { }
}
