namespace AlFalah.Application.Common;

/// <summary>
/// Thrown by services when an authenticated school-scoped caller (School Manager /
/// Moderator / Instructor) tries to read or mutate data that belongs to a school
/// other than the one encoded in their token's ActiveSchoolId claim.
///
/// Mapped to HTTP <c>403 Forbidden</c> with an Arabic message by the
/// <see cref="AlFalah.Api.Middlewares.GlobalExceptionMiddleware"/>.
/// </summary>
public class UnauthorizedSchoolAccessException : Exception
{
    public UnauthorizedSchoolAccessException(string message)
        : base(message) { }

    /// <summary>Convenience factory used by services.</summary>
    public static UnauthorizedSchoolAccessException OutsideScope(int? activeSchoolId, int requestedSchoolId)
        => new($"لا تملك صلاحية الوصول إلى بيانات خارج المدرسة الحالية ({activeSchoolId?.ToString() ?? "بدون"}). المدرسة المطلوبة: {requestedSchoolId}.");
}
