using AlFalah.Application.IntelligentTimetable.DTOs;
using FluentValidation;

namespace AlFalah.Application.Validators.IntelligentTimetable;

public sealed class TeacherAvailabilityValidator : AbstractValidator<UpdateTeacherProfileRequest>
{
    public TeacherAvailabilityValidator()
    {
        RuleFor(x => x.Revision).GreaterThanOrEqualTo(0);
        RuleFor(x => x.BellScheduleRevisionId).GreaterThan(0);
        RuleFor(x => x.ShortDisplayName).NotNull().MaximumLength(60);
        RuleFor(x => x.MaximumWeeklyPeriods).GreaterThanOrEqualTo(0).WithMessage("أقصى عدد للحصص يجب أن يكون صفراً أو أكثر.");
        RuleFor(x => x.Slots).NotNull();
        When(x => x.Slots != null, () => {
            RuleFor(x => x.Slots).Must(x => x.All(s => s != null) && x.Select(s => (s.Day, s.BellPeriodId)).Distinct().Count() == x.Count)
                .WithMessage("تحتوي الإتاحة على حصص مكررة أو غير صالحة.");
            RuleFor(x => x).Must(x => x.MaximumWeeklyPeriods <= x.Slots.Count(s => s != null && s.IsAvailable))
                .WithMessage("أقصى عدد للحصص لا يمكن أن يتجاوز عدد الحصص المتاحة.");
        });
    }
}
