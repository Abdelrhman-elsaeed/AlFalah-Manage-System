using AlFalah.Application.DTOs.Auth;
using FluentValidation;

namespace AlFalah.Application.Validators.Auth;

public sealed class ChangePasswordValidator : AbstractValidator<ChangePasswordRequestDto>
{
    public ChangePasswordValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty().WithMessage("كلمة المرور الحالية مطلوبة.");
        RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(6)
            .WithMessage("كلمة المرور الجديدة يجب ألا تقل عن 6 خانات.");
    }
}
