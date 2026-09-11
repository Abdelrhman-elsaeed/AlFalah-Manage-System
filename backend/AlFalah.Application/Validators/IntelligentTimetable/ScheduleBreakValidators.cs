using AlFalah.Application.IntelligentTimetable.DTOs;
using FluentValidation;

namespace AlFalah.Application.Validators.IntelligentTimetable;

public sealed class ScheduleBreakValidator : AbstractValidator<ScheduleBreakDto>
{
    public static readonly string[] Categories = ["Recess", "Prayer", "Meal", "Assembly", "Other"];
    public ScheduleBreakValidator()
    {
        RuleFor(x => x.Name).NotEmpty().Must(x => !string.IsNullOrWhiteSpace(x)).MaximumLength(120)
            .WithMessage("اسم الاستراحة مطلوب ولا يتجاوز 120 حرفاً.");
        RuleFor(x => x.Category).Must(x => x is null || Categories.Contains(x)).WithMessage("فئة الاستراحة غير صالحة.");
        RuleFor(x => x.EndLocalTime).GreaterThan(x => x.StartLocalTime).WithMessage("بداية الاستراحة يجب أن تسبق نهايتها دون عبور منتصف الليل.");
    }
}

public sealed class SaveScheduleBreaksValidator : AbstractValidator<SaveScheduleBreaksRequest>
{
    public SaveScheduleBreaksValidator()
    {
        RuleFor(x => x.Revision).GreaterThan(0);
        RuleFor(x => x.DefaultBreaks).NotNull();
        RuleFor(x => x.Days).Must(days => days is not null && days.Count == 7 &&
            days.Select(x => x?.Day ?? -1).Order().SequenceEqual(Enumerable.Range(1, 7)))
            .WithMessage("يجب تحديد أيام الأسبوع السبعة مرة واحدة.");
        RuleForEach(x => x.Days).ChildRules(day => day.RuleFor(x => x.Breaks).NotNull());
    }
}

/// <summary>Validate the complete effective day so a multi-day change is all-or-nothing.</summary>
public static class ScheduleBreakValidation
{
    public static void Validate(IReadOnlyList<ScheduleBreakDto>? breaks, IReadOnlyList<BellPeriodDto>? periods,
        string breakPath, string periodPath, ValidationContext<SaveBellScheduleRequest> context)
    {
        var rows = breaks ?? [];
        var validator = new ScheduleBreakValidator();
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            if (row is null) { context.AddFailure($"{breakPath}[{i}]", "بيانات الاستراحة مطلوبة."); continue; }
            foreach (var failure in validator.Validate(row).Errors)
                context.AddFailure($"{breakPath}[{i}].{failure.PropertyName}", failure.ErrorMessage);
            foreach (var period in periods ?? [])
            {
                if (period is null || !Overlaps(row.StartLocalTime, row.EndLocalTime, period.StartLocalTime, period.EndLocalTime)) continue;
                var message = $"الاستراحة «{row.Name}» تتداخل مع الحصة {period.Sequence}.";
                context.AddFailure($"{breakPath}[{i}]", message);
                context.AddFailure($"{periodPath}[{period.Sequence}]", message);
            }
            for (var j = 0; j < i; j++)
            {
                var other = rows[j];
                if (other is null || !Overlaps(row.StartLocalTime, row.EndLocalTime, other.StartLocalTime, other.EndLocalTime)) continue;
                var message = $"الاستراحة «{row.Name}» تتداخل مع «{other.Name}».";
                context.AddFailure($"{breakPath}[{i}]", message);
                context.AddFailure($"{breakPath}[{j}]", message);
            }
        }
    }

    public static bool Overlaps(TimeOnly start, TimeOnly end, TimeOnly otherStart, TimeOnly otherEnd) =>
        start < otherEnd && otherStart < end;
}
