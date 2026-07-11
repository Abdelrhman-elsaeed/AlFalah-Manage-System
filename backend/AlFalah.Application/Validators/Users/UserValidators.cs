using AlFalah.Application.DTOs.Users;
using AlFalah.Domain.Enums;
using FluentValidation;

namespace AlFalah.Application.Validators.Users;

public class UserCreateRequestValidator : AbstractValidator<UserCreateRequestDto>
{
    public UserCreateRequestValidator()
    {
        RuleFor(x => x.Username)
            .NotEmpty().WithMessage("اسم المستخدم مطلوب.")
            .MinimumLength(3).WithMessage("يجب ألا يقل اسم المستخدم عن 3 أحرف.")
            .MaximumLength(256).WithMessage("يجب ألا يتجاوز اسم المستخدم 256 حرفاً.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("كلمة المرور مطلوبة.")
            .MinimumLength(8).WithMessage("يجب ألا تقل كلمة المرور عن 8 أحرف.");

        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("الاسم الأول مطلوب.")
            .MaximumLength(100);

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("اسم العائلة مطلوب.")
            .MaximumLength(100);

        RuleFor(x => x.Email)
            .EmailAddress().When(x => !string.IsNullOrEmpty(x.Email))
            .WithMessage("البريد الإلكتروني غير صالح.");

        RuleFor(x => x.PreferredLanguage)
            .Must(l => l == "ar" || l == "en")
            .WithMessage("اللغة المفضلة يجب أن تكون 'ar' أو 'en'.");

        // Phase 2 scope: only Instructor / Moderator / SchoolManager are creatable here.
        RuleFor(x => x.Role)
            .NotEmpty().WithMessage("الدور مطلوب.")
            .Must(BeInPhaseTwoScope)
            .WithMessage("الدور يجب أن يكون أحد: SchoolManager أو Moderator أو Instructor.");

        RuleFor(x => x.SchoolId)
            .GreaterThan(0).When(x => x.SchoolId.HasValue)
            .WithMessage("معرّف المدرسة غير صالح.");
    }

    private static bool BeInPhaseTwoScope(string role) =>
        role == RoleNames.SchoolManager
        || role == RoleNames.Moderator
        || role == RoleNames.Instructor;
}

public class UserUpdateRequestValidator : AbstractValidator<UserUpdateRequestDto>
{
    public UserUpdateRequestValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrEmpty(x.Email));
        RuleFor(x => x.PreferredLanguage).Must(l => l == "ar" || l == "en");
    }
}

public class UserListQueryValidator : AbstractValidator<UserListQuery>
{
    public UserListQueryValidator()
    {
        RuleFor(x => x.Role).Must(r =>
            string.IsNullOrEmpty(r) ||
            r == RoleNames.SuperAdmin ||
            r == RoleNames.MainManager ||
            r == RoleNames.SchoolManager ||
            r == RoleNames.Moderator ||
            r == RoleNames.Instructor
        ).WithMessage("قيمة الدور غير صالحة.");

        RuleFor(x => x.SchoolId).GreaterThan(0).When(x => x.SchoolId.HasValue);
    }
}