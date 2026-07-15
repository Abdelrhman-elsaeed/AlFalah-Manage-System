using AlFalah.Application.DTOs.ImprovementPlans;
using FluentValidation;

namespace AlFalah.Application.Validators.ImprovementPlans;

public class CreatePlanRequestDtoValidator : AbstractValidator<CreatePlanRequestDto>
{
    public CreatePlanRequestDtoValidator()
    {
        RuleFor(x => x.VisitId)
            .GreaterThan(0).WithMessage("معرف الزيارة غير صحيح.");

        RuleFor(x => x.Goal)
            .NotEmpty().WithMessage("الهدف مطلوب.")
            .MaximumLength(2000).WithMessage("الهدف يجب أن لا يتجاوز 2000 حرف.");

        RuleFor(x => x.Actions)
            .NotEmpty().WithMessage("الإجراءات مطلوبة.")
            .MaximumLength(4000).WithMessage("الإجراءات يجب أن لا تتجاوز 4000 حرف.");

        RuleFor(x => x.StartDate)
            .NotEmpty().WithMessage("تاريخ البدء مطلوب.");

        RuleFor(x => x.EndDate)
            .NotEmpty().WithMessage("تاريخ الانتهاء مطلوب.");

        RuleFor(x => x.SuccessIndicators)
            .NotEmpty().WithMessage("مؤشرات النجاح مطلوبة.")
            .MaximumLength(2000).WithMessage("مؤشرات النجاح يجب أن لا تتجاوز 2000 حرف.");
    }
}

public class UpdatePlanRequestDtoValidator : AbstractValidator<UpdatePlanRequestDto>
{
    public UpdatePlanRequestDtoValidator()
    {
        RuleFor(x => x.Goal)
            .NotEmpty().WithMessage("الهدف مطلوب.")
            .MaximumLength(2000).WithMessage("الهدف يجب أن لا يتجاوز 2000 حرف.");

        RuleFor(x => x.Actions)
            .NotEmpty().WithMessage("الإجراءات مطلوبة.")
            .MaximumLength(4000).WithMessage("الإجراءات يجب أن لا تتجاوز 4000 حرف.");

        RuleFor(x => x.StartDate)
            .NotEmpty().WithMessage("تاريخ البدء مطلوب.");

        RuleFor(x => x.EndDate)
            .NotEmpty().WithMessage("تاريخ الانتهاء مطلوب.");

        RuleFor(x => x.SuccessIndicators)
            .NotEmpty().WithMessage("مؤشرات النجاح مطلوبة.")
            .MaximumLength(2000).WithMessage("مؤشرات النجاح يجب أن لا تتجاوز 2000 حرف.");

        RuleFor(x => x.Status)
            .NotEmpty().WithMessage("حالة الخطة مطلوبة.")
            .Must(s => s == "active" || s == "completed" || s == "cancelled")
            .WithMessage("حالة الخطة غير صحيحة.");
    }
}

public class CreateFollowUpRequestDtoValidator : AbstractValidator<CreateFollowUpRequestDto>
{
    public CreateFollowUpRequestDtoValidator()
    {
        RuleFor(x => x.FollowDate)
            .NotEmpty().WithMessage("تاريخ المتابعة مطلوب.");

        RuleFor(x => x.ProgressNote)
            .NotEmpty().WithMessage("ملاحظة التقدم مطلوبة.")
            .MaximumLength(2000).WithMessage("ملاحظة التقدم يجب أن لا تتجاوز 2000 حرف.");

        RuleFor(x => x.EvidenceNote)
            .MaximumLength(2000).WithMessage("دليل التقدم يجب أن لا يتجاوز 2000 حرف.")
            .When(x => !string.IsNullOrEmpty(x.EvidenceNote));

        RuleFor(x => x.ProgressScore)
            .InclusiveBetween(0, 100).WithMessage("نسبة التقدم يجب أن تكون بين 0 و 100.")
            .When(x => x.ProgressScore.HasValue);
    }
}

public class UpdateFollowUpRequestDtoValidator : AbstractValidator<UpdateFollowUpRequestDto>
{
    public UpdateFollowUpRequestDtoValidator()
    {
        RuleFor(x => x.FollowDate)
            .NotEmpty().WithMessage("تاريخ المتابعة مطلوب.");

        RuleFor(x => x.ProgressNote)
            .NotEmpty().WithMessage("ملاحظة التقدم مطلوبة.")
            .MaximumLength(2000).WithMessage("ملاحظة التقدم يجب أن لا تتجاوز 2000 حرف.");

        RuleFor(x => x.EvidenceNote)
            .MaximumLength(2000).WithMessage("دليل التقدم يجب أن لا يتجاوز 2000 حرف.")
            .When(x => !string.IsNullOrEmpty(x.EvidenceNote));

        RuleFor(x => x.ProgressScore)
            .InclusiveBetween(0, 100).WithMessage("نسبة التقدم يجب أن تكون بين 0 و 100.")
            .When(x => x.ProgressScore.HasValue);
    }
}
