using AlFalah.Application.DTOs.Teachers;
using FluentValidation;

namespace AlFalah.Application.Validators.Teachers;

/// <summary>
/// D-74 — Validator for the body of the two PUT /teaching endpoints.
/// Same payload shape used by both /api/v1/account/teaching and
/// /api/v1/teachers/{userId}/teaching. The controller layer enforces
/// SELF-ONLY vs manager-scoped; this validator just enforces shape.
/// </summary>
public class TeacherTeachingUpsertRequestValidator : AbstractValidator<TeacherTeachingUpsertRequest>
{
    public TeacherTeachingUpsertRequestValidator()
    {
        RuleFor(x => x.Subject)
            .MaximumLength(200).When(r => !string.IsNullOrEmpty(r.Subject))
            .WithMessage("يجب ألا تتجاوز المادة الدراسية 200 حرف.");

        // Stage enum is optional (the form may not send it; null = no change).

        When(x => x.Classes != null, () =>
        {
            RuleForEach(x => x.Classes!)
                .NotEmpty().WithMessage("لا يمكن إضافة صف فارغ.")
                .MaximumLength(50).WithMessage("يجب ألا يتجاوز اسم الصف 50 حرفاً.");
            RuleFor(x => x.Classes!.Count)
                .LessThanOrEqualTo(50)
                .WithMessage("لا يمكن إضافة أكثر من 50 صفاً.");
        });
    }
}
