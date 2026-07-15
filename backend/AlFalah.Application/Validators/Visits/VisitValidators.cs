using AlFalah.Application.DTOs.Visits;
using AlFalah.Domain.Enums;
using FluentValidation;

namespace AlFalah.Application.Validators.Visits;

public class CreateVisitRequestValidator : AbstractValidator<CreateVisitRequestDto>
    {
        public CreateVisitRequestValidator()
        {
            RuleFor(x => x.InstructorId)
                .NotEmpty().WithMessage("يجب اختيار المعلم المُقيَّم.");

            RuleFor(x => x.VisitCategory)
                .Must(v => Enum.IsDefined(typeof(VisitCategory), v))
                .WithMessage("نوع الزيارة غير صحيح.");

            RuleFor(x => x.VisitSequence)
                .Must(v => Enum.IsDefined(typeof(VisitSequence), v))
                .WithMessage("تسلسل الزيارة غير صحيح.");

            RuleFor(x => x.VisitDate)
                .NotEmpty().WithMessage("تاريخ الزيارة مطلوب.");

            RuleFor(x => x.Subject)
                .NotEmpty().WithMessage("المادة الدراسية مطلوبة.")
                .MaximumLength(200).WithMessage("المادة يجب أن لا تتجاوز 200 حرف.");

            RuleFor(x => x.GradeClass)
                .NotEmpty().WithMessage("الصف الدراسي مطلوب.")
                .MaximumLength(100).WithMessage("الصف يجب أن لا يتجاوز 100 حرف.");

            RuleFor(x => x.LessonTitle)
                .NotEmpty().WithMessage("عنوان الدرس مطلوب.")
                .MaximumLength(300).WithMessage("عنوان الدرس يجب أن لا يتجاوز 300 حرف.");

            RuleFor(x => x.PresentCount)
                .GreaterThanOrEqualTo(0).WithMessage("عدد الحاضرين يجب أن يكون صفراً أو أكثر.");

            RuleFor(x => x.AbsentCount)
                .GreaterThanOrEqualTo(0).When(x => x.AbsentCount.HasValue)
                .WithMessage("عدد الغائبين يجب أن يكون صفراً أو أكثر.");

            RuleFor(x => x.Notes)
                .MaximumLength(2000).WithMessage("الملاحظات يجب أن لا تتجاوز 2000 حرف.");

            // Optional initial scores
            RuleForEach(x => x.Scores).SetValidator(new VisitScoreInputValidator())
                .When(x => x.Scores != null && x.Scores.Count > 0);

        }
    }

    public class UpdateVisitRequestValidator : AbstractValidator<UpdateVisitRequestDto>
    {
        public UpdateVisitRequestValidator()
        {
            RuleFor(x => x.VisitCategory)
                .Must(v => Enum.IsDefined(typeof(VisitCategory), v))
                .WithMessage("نوع الزيارة غير صحيح.");

            RuleFor(x => x.VisitSequence)
                .Must(v => Enum.IsDefined(typeof(VisitSequence), v))
                .WithMessage("تسلسل الزيارة غير صحيح.");

            RuleFor(x => x.VisitDate)
                .NotEmpty().WithMessage("تاريخ الزيارة مطلوب.");

            RuleFor(x => x.Subject)
                .NotEmpty().WithMessage("المادة الدراسية مطلوبة.")
                .MaximumLength(200).WithMessage("المادة يجب أن لا تتجاوز 200 حرف.");

            RuleFor(x => x.GradeClass)
                .NotEmpty().WithMessage("الصف الدراسي مطلوب.")
                .MaximumLength(100).WithMessage("الصف يجب أن لا يتجاوز 100 حرف.");

            RuleFor(x => x.LessonTitle)
                .NotEmpty().WithMessage("عنوان الدرس مطلوب.")
                .MaximumLength(300).WithMessage("عنوان الدرس يجب أن لا يتجاوز 300 حرف.");

            RuleFor(x => x.PresentCount)
                .GreaterThanOrEqualTo(0).WithMessage("عدد الحاضرين يجب أن يكون صفراً أو أكثر.");

            RuleFor(x => x.AbsentCount)
                .GreaterThanOrEqualTo(0).When(x => x.AbsentCount.HasValue)
                .WithMessage("عدد الغائبين يجب أن يكون صفراً أو أكثر.");

            RuleFor(x => x.Notes)
                .MaximumLength(2000).WithMessage("الملاحظات يجب أن لا تتجاوز 2000 حرف.");

            // The service compares this collection with the visit's snapshotted
            // rubric. Rubric versions are dynamic, so validation must not assume 25.
            RuleFor(x => x.Scores)
                .NotNull().WithMessage("يجب إرسال درجات المعايير.");

            RuleForEach(x => x.Scores).SetValidator(new VisitScoreInputValidator());
        }
    }

public class VisitScoreInputValidator : AbstractValidator<VisitScoreInputDto>
{
    public VisitScoreInputValidator()
    {
        RuleFor(x => x.RubricStandardId)
            .GreaterThan(0).WithMessage("رمز المعيار غير صحيح.");

        RuleFor(x => x.Score)
            .InclusiveBetween(0, 4)
            .When(x => x.Score.HasValue)
            .WithMessage("درجة المعيار يجب أن تكون بين 0 و 4.");

        RuleFor(x => x.EvidenceNote)
            .MaximumLength(2000).WithMessage("الدليل يجب أن لا يتجاوز 2000 حرف.");
    }
}

public class VisitListQueryValidator : AbstractValidator<VisitListQuery>
{
    public VisitListQueryValidator()
    {
        RuleFor(x => x.Status)
            .Must(v => !v.HasValue || Enum.IsDefined(typeof(VisitStatus), v.Value))
            .WithMessage("حالة الزيارة غير صحيحة.");

        RuleFor(x => x.VisitCategory)
            .Must(v => !v.HasValue || Enum.IsDefined(typeof(VisitCategory), v.Value))
            .WithMessage("نوع الزيارة غير صحيح.");
    }
}

/// <summary>POST /api/v1/visits/{id}/reject — reason required, ≤ 1000 chars.</summary>
public class RejectVisitRequestValidator : AbstractValidator<RejectVisitRequestDto>
{
    public RejectVisitRequestValidator()
    {
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("سبب الرفض مطلوب.")
            .MaximumLength(1000).WithMessage("سبب الرفض يجب أن لا يتجاوز 1000 حرف.");
    }
}

/// <summary>POST /api/v1/visits/{id}/reopen — reason required, ≤ 1000 chars.</summary>
public class ReopenVisitRequestValidator : AbstractValidator<ReopenVisitRequestDto>
{
    public ReopenVisitRequestValidator()
    {
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("سبب إعادة الفتح مطلوب.")
            .MaximumLength(1000).WithMessage("سبب إعادة الفتح يجب أن لا يتجاوز 1000 حرف.");
    }
}
