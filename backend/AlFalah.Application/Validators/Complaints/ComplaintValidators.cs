using AlFalah.Application.DTOs.Complaints;
using FluentValidation;

namespace AlFalah.Application.Validators.Complaints;

public class CreateComplaintRequestValidator : AbstractValidator<CreateComplaintRequestDto>
{
    public CreateComplaintRequestValidator()
    {
        RuleFor(x => x.Subject)
            .NotEmpty().WithMessage("موضوع الشكوى مطلوب.")
            .MaximumLength(200).WithMessage("موضوع الشكوى يجب ألا يتجاوز 200 حرف.");

        RuleFor(x => x.Body)
            .NotEmpty().WithMessage("نص الشكوى مطلوب.")
            .MaximumLength(4000).WithMessage("نص الشكوى يجب ألا يتجاوز 4000 حرف.");
    }
}

public class UpdateComplaintStatusRequestValidator : AbstractValidator<UpdateComplaintStatusRequestDto>
{
    public UpdateComplaintStatusRequestValidator()
    {
        RuleFor(x => x.Status)
            .InclusiveBetween(1, 5).WithMessage("حالة الشكوى غير صالحة.");

        RuleFor(x => x.ResolutionNote)
            .MaximumLength(2000).WithMessage("ملاحظة المعالجة يجب ألا تتجاوز 2000 حرف.");
    }
}

public class ReopenVisitFromComplaintRequestValidator : AbstractValidator<ReopenVisitFromComplaintRequestDto>
{
    public ReopenVisitFromComplaintRequestValidator()
    {
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("سبب إعادة الفتح مطلوب.")
            .MaximumLength(1000).WithMessage("سبب إعادة الفتح يجب ألا يتجاوز 1000 حرف.");
    }
}
