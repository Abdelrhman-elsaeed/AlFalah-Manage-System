using AlFalah.Application.DTOs.Schools;
using FluentValidation;

namespace AlFalah.Application.Validators.Schools;

public sealed class SchoolLocationCreateRequestValidator : AbstractValidator<SchoolLocationCreateRequestDto>
{
    public SchoolLocationCreateRequestValidator()
    {
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(120);
        RuleFor(x => x.NameEn).MaximumLength(120);
        RuleFor(x => x.RegionNameAr).NotEmpty().MaximumLength(120);
        RuleFor(x => x.RegionNameEn).MaximumLength(120);
        RuleFor(x => x.Latitude)
            .InclusiveBetween(16m, 33m)
            .WithMessage("خط العرض يجب أن يكون داخل حدود المملكة العربية السعودية.");
        RuleFor(x => x.Longitude)
            .InclusiveBetween(34m, 56m)
            .WithMessage("خط الطول يجب أن يكون داخل حدود المملكة العربية السعودية.");
    }
}
