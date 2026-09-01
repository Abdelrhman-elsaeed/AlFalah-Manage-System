namespace AlFalah.Application.StudentAffairs.Attendance.Handlers;

public static class AttendanceHandlerSupport
{
    public const string AuthenticationRequired = "An authenticated user and active school are required";
    public const string PermissionDenied = "You do not have permission to perform this action";
    public const string ConcurrencyConflict = "Absence excuse was modified by another user";

    public static bool TryDecodeExpectedRowVersion(
        string encodedRowVersion,
        byte[] currentRowVersion,
        out byte[] expectedRowVersion)
    {
        expectedRowVersion = Array.Empty<byte>();
        if (string.IsNullOrWhiteSpace(encodedRowVersion)) return false;

        try
        {
            expectedRowVersion = Convert.FromBase64String(encodedRowVersion);
            return expectedRowVersion.Length > 0
                && currentRowVersion.AsSpan().SequenceEqual(expectedRowVersion);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
