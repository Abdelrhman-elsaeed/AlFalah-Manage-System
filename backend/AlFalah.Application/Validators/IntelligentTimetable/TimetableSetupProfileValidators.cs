using AlFalah.Application.IntelligentTimetable.DTOs;
using FluentValidation;

namespace AlFalah.Application.Validators.IntelligentTimetable;

public sealed class CreateTimetableSetupProfileRequestValidator
    : AbstractValidator<CreateTimetableSetupProfileRequest>
{
    public CreateTimetableSetupProfileRequestValidator()
    {
        RuleFor(x => x.AcademicYearId).GreaterThan(0).WithMessage("اختر عاماً دراسياً صالحاً.");
        RuleFor(x => x.Semester).IsInEnum().WithMessage("اختر فصلاً دراسياً صالحاً.");
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("اسم ملف الإعداد مطلوب.")
            .MaximumLength(120).WithMessage("اسم ملف الإعداد يجب ألا يتجاوز 120 حرفاً.");
    }
}

public sealed class UpdateTimetableSetupProfileRequestValidator
    : AbstractValidator<UpdateTimetableSetupProfileRequest>
{
    public UpdateTimetableSetupProfileRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("اسم ملف الإعداد مطلوب.")
            .MaximumLength(120).WithMessage("اسم ملف الإعداد يجب ألا يتجاوز 120 حرفاً.");
        RuleFor(x => x.Revision).GreaterThan(0).WithMessage("رقم مراجعة ملف الإعداد غير صالح.");
    }
}
