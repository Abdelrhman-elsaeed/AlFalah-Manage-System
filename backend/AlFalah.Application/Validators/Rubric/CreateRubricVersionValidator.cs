using AlFalah.Application.DTOs.Rubric;
using FluentValidation;

namespace AlFalah.Application.Validators.Rubric;

public class CreateRubricVersionValidator : AbstractValidator<CreateRubricVersionDto>
{
    public CreateRubricVersionValidator()
    {
        RuleFor(x => x.Notes)
            .MaximumLength(2000).WithMessage("الملاحظات يجب أن لا تتجاوز 2000 حرف.");

        RuleFor(x => x.Domains)
            .NotEmpty().WithMessage("يجب إدخال محور واحد على الأقل.")
            .Must(d => d.Count <= 20).WithMessage("لا يمكن أن تتجاوز المحاور 20 محوراً.");

        RuleForEach(x => x.Domains).SetValidator(new RubricDomainWriteValidator());
    }
}

public class RubricDomainWriteValidator : AbstractValidator<RubricDomainWriteDto>
{
    public RubricDomainWriteValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("رمز المحور مطلوب.")
            .MaximumLength(20).WithMessage("رمز المحور يجب أن لا يتجاوز 20 حرفاً.");

        RuleFor(x => x.NameAr)
            .NotEmpty().WithMessage("اسم المحور بالعربية مطلوب.")
            .MaximumLength(300).WithMessage("اسم المحور يجب أن لا يتجاوز 300 حرف.");

        RuleFor(x => x.SortOrder)
            .GreaterThan(0).WithMessage("ترتيب المحور يجب أن يكون أكبر من الصفر.");

        RuleFor(x => x.Standards)
            .NotEmpty().WithMessage("يجب إدخال معيار واحد على الأقل لكل محور.")
            .Must(s => s.Count <= 50).WithMessage("لا يمكن أن تتجاوز المعايير 50 معياراً لكل محور.");

        RuleForEach(x => x.Standards).SetValidator(new RubricStandardWriteValidator());
    }
}

public class RubricStandardWriteValidator : AbstractValidator<RubricStandardWriteDto>
{
    public RubricStandardWriteValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("رمز المعيار مطلوب.")
            .MaximumLength(20).WithMessage("رمز المعيار يجب أن لا يتجاوز 20 حرفاً.");

        RuleFor(x => x.TextAr)
            .NotEmpty().WithMessage("نص المعيار بالعربية مطلوب.")
            .MaximumLength(1000).WithMessage("نص المعيار يجب أن لا يتجاوز 1000 حرف.");

        RuleFor(x => x.SortOrder)
            .GreaterThan(0).WithMessage("ترتيب المعيار يجب أن يكون أكبر من الصفر.");
    }
}
