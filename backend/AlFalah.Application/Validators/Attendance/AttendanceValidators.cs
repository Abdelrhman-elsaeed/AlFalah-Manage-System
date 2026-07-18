using AlFalah.Application.DTOs.Attendance;
using AlFalah.Domain.Enums;
using FluentValidation;

namespace AlFalah.Application.Validators.Attendance;

public class SaveAttendanceSheetRequestValidator : AbstractValidator<SaveAttendanceSheetRequestDto>
{
    public SaveAttendanceSheetRequestValidator()
    {
        RuleFor(x => x.Date).Must(BeSchoolWorkDay)
            .WithMessage("الحضور يُسجّل في أيام العمل من الأحد إلى الخميس فقط.");
        RuleFor(x => x.Entries).NotEmpty();
        RuleForEach(x => x.Entries).ChildRules(entry =>
        {
            entry.RuleFor(x => x.UserId).NotEmpty();
            entry.RuleFor(x => x.Status).IsInEnum();
            entry.RuleFor(x => x.Notes).MaximumLength(500);
        });
        RuleFor(x => x.Entries.Select(e => e.UserId).Distinct().Count())
            .Equal(x => x.Entries.Count)
            .WithMessage("لا يمكن تسجيل نفس الموظف أكثر من مرة في نفس اليوم.");
    }

    internal static bool BeSchoolWorkDay(DateOnly date) =>
        date.DayOfWeek is not DayOfWeek.Friday and not DayOfWeek.Saturday;
}
