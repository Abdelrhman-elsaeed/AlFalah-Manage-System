using AlFalah.Application.DTOs.UserSchoolRoles;
using AlFalah.Domain.Enums;
using FluentValidation;

namespace AlFalah.Application.Validators.UserSchoolRoles;

public class UserSchoolRoleCreateRequestValidator : AbstractValidator<UserSchoolRoleCreateRequestDto>
{
    public UserSchoolRoleCreateRequestValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("معرّف المستخدم مطلوب.");

        RuleFor(x => x.SchoolId)
            .GreaterThan(0).WithMessage("معرّف المدرسة غير صالح.");

        RuleFor(x => x.Role)
            .NotEmpty().WithMessage("الدور مطلوب.")
            .Must(BeInPhaseTwoScope)
            .WithMessage("الدور يجب أن يكون أحد: SchoolManager أو Moderator أو Instructor.");
    }

    private static bool BeInPhaseTwoScope(string role) =>
        role == RoleNames.SchoolManager
        || role == RoleNames.Moderator
        || role == RoleNames.Instructor;
}