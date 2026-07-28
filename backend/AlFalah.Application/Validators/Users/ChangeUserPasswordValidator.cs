using AlFalah.Application.DTOs.Users;
using FluentValidation;

namespace AlFalah.Application.Validators.Users;

public sealed class ChangeUserPasswordValidator : AbstractValidator<ChangeUserPasswordRequestDto>
{
    public ChangeUserPasswordValidator()
    {
        RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(8)
            .WithMessage("كلمة المرور الجديدة يجب ألا تقل عن 8 أحرف.");
    }
}
