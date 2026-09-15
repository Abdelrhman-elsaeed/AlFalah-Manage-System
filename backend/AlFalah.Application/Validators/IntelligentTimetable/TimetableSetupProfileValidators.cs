using AlFalah.Application.IntelligentTimetable.DTOs;
using FluentValidation;

namespace AlFalah.Application.Validators.IntelligentTimetable;

public sealed class CreateTimetableAcademicYearRequestValidator
    : AbstractValidator<CreateTimetableAcademicYearRequest>
{
    public CreateTimetableAcademicYearRequestValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("كود العام الدراسي مطلوب.")
            .MaximumLength(32).WithMessage("كود العام الدراسي يجب ألا يتجاوز 32 حرفاً.");
        RuleFor(x => x.NameAr)
            .NotEmpty().WithMessage("اسم العام الدراسي مطلوب.")
            .MaximumLength(128).WithMessage("اسم العام الدراسي يجب ألا يتجاوز 128 حرفاً.");
        RuleFor(x => x.EndsOn)
            .GreaterThan(x => x.StartsOn).WithMessage("نهاية العام الدراسي يجب أن تكون بعد بدايته.");
        RuleFor(x => x.ActiveSemester)
            .IsInEnum().WithMessage("اختر الفصل الدراسي النشط.");
        RuleFor(x => x).Custom((request, context) =>
        {
            ValidateTerm(
                context,
                "FirstSemester",
                "الفصل الدراسي الأول",
                request.FirstSemesterStartsOn,
                request.FirstSemesterEndsOn,
                request.StartsOn,
                request.EndsOn);
            ValidateTerm(
                context,
                "SecondSemester",
                "الفصل الدراسي الثاني",
                request.SecondSemesterStartsOn,
                request.SecondSemesterEndsOn,
                request.StartsOn,
                request.EndsOn);

            if (request.FirstSemesterEndsOn >= request.SecondSemesterStartsOn)
                context.AddFailure("SecondSemesterStartsOn", "يجب أن يبدأ الفصل الدراسي الثاني بعد انتهاء الفصل الأول.");
        });
    }

    private static void ValidateTerm(
        ValidationContext<CreateTimetableAcademicYearRequest> context,
        string propertyPrefix,
        string label,
        DateOnly startsOn,
        DateOnly endsOn,
        DateOnly yearStartsOn,
        DateOnly yearEndsOn)
    {
        if (endsOn <= startsOn)
            context.AddFailure($"{propertyPrefix}EndsOn", $"نهاية {label} يجب أن تكون بعد بدايته.");
        if (startsOn < yearStartsOn || endsOn > yearEndsOn)
            context.AddFailure($"{propertyPrefix}StartsOn", $"تواريخ {label} يجب أن تقع داخل العام الدراسي.");
    }
}

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
