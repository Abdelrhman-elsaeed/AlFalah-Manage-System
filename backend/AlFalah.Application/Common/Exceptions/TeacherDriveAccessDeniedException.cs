namespace AlFalah.Application.Common.Exceptions;

public sealed class TeacherDriveAccessDeniedException : Exception
{
    public TeacherDriveAccessDeniedException(string message = "ليس لديك صلاحية للوصول إلى هذا المجلد.") : base(message) { }
}
