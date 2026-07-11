using AlFalah.Application.DTOs.Schools;
using AlFalah.Domain.Enums;
using FluentValidation;

namespace AlFalah.Application.Validators.Schools;

public class SchoolCreateRequestValidator : AbstractValidator<SchoolCreateRequestDto>
{
    public SchoolCreateRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("اسم المدرسة مطلوب.")
            .MaximumLength(200).WithMessage("يجب ألا يتجاوز اسم المدرسة 200 حرف.");

        RuleFor(x => x.City)
            .NotEmpty().WithMessage("المدينة مطلوبة.")
            .MaximumLength(100).WithMessage("يجب ألا يتجاوز اسم المدينة 100 حرف.");

        RuleFor(x => x.LocationDetails)
            .MaximumLength(500).WithMessage("يجب ألا تتجاوز تفاصيل الموقع 500 حرف.");

        RuleFor(x => x.LogoUrl)
            .MaximumLength(1000).WithMessage("يجب ألا يتجاوز رابط الشعار 1000 حرف.");

        RuleFor(x => x.Stage)
            .NotEmpty().WithMessage("المرحلة الدراسية مطلوبة.")
            .Must(BeAValidStage).WithMessage("قيمة المرحلة الدراسية غير صالحة.");

        // Manager is OPTIONAL at create time, but if provided must be non-empty.
        When(x => !string.IsNullOrEmpty(x.ManagerUserId), () =>
        {
            RuleFor(x => x.ManagerUserId!)
                .MaximumLength(450)
                .WithMessage("معرّف المستخدم غير صالح.");
        });
    }

    private static bool BeAValidStage(string stage) =>
        Enum.TryParse<SchoolStage>(stage, ignoreCase: false, out _);
}

public class SchoolUpdateRequestValidator : AbstractValidator<SchoolUpdateRequestDto>
{
    public SchoolUpdateRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("اسم المدرسة مطلوب.")
            .MaximumLength(200);

        RuleFor(x => x.City)
            .NotEmpty().WithMessage("المدينة مطلوبة.")
            .MaximumLength(100);

        RuleFor(x => x.LocationDetails)
            .MaximumLength(500);

        RuleFor(x => x.LogoUrl)
            .MaximumLength(1000);

        RuleFor(x => x.Stage)
            .NotEmpty()
            .Must(BeAValidStage).WithMessage("قيمة المرحلة الدراسية غير صالحة.");
    }

    private static bool BeAValidStage(string stage) =>
        Enum.TryParse<SchoolStage>(stage, ignoreCase: false, out _);
}

public class AssignSchoolManagerRequestValidator : AbstractValidator<AssignSchoolManagerRequestDto>
{
    public AssignSchoolManagerRequestValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("معرّف المستخدم مطلوب.");
    }
}

public class SchoolListQueryValidator : AbstractValidator<SchoolListQuery>
{
    public SchoolListQueryValidator()
    {
        RuleFor(x => x.Stage)
            .Must(s => string.IsNullOrEmpty(s) || Enum.TryParse<SchoolStage>(s, false, out _))
            .WithMessage("قيمة المرحلة الدراسية غير صالحة.");
    }
}