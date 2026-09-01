using System;
using AlFalah.Application.Interfaces;
using AlFalah.Domain.Enums;

namespace AlFalah.Application.StudentAffairs.Students.Handlers;

public static class StudentHandlerSupport
{
    public const string AuthenticationRequired = "Authentication is required to perform student affairs operations";
    public const string PermissionDenied = "You do not have permission to perform this student affairs operation";
    public const string NotFound = "The requested student affairs record was not found";
    public const string StudentNotFound = "Student not found";
    public const string GuardianNotFound = "Guardian profile not found";
    public const string DuplicateStudentNumber = "A student with this student number already exists";
    public const string DuplicateNationalId = "A student with this national ID already exists";
    public const string ConcurrencyConflict = "The record was modified by another operation. Please refresh and try again.";

    public static bool HasAnyPermission(ICurrentUserService user, params string[] permissionNames)
    {
        foreach (var permission in permissionNames)
        {
            if (user.HasPermission(permission)) return true;
        }
        return false;
    }
}
