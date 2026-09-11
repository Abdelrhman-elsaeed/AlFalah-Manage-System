using AlFalah.Application.IntelligentTimetable.DTOs;
using FluentValidation;

namespace AlFalah.Application.Validators.IntelligentTimetable;

public sealed class SaveBellScheduleValidator : AbstractValidator<SaveBellScheduleRequest>
{
    public SaveBellScheduleValidator()
    {
        RuleFor(x => x.Name).NotEmpty().Must(x => !string.IsNullOrWhiteSpace(x)).MaximumLength(120);
        RuleFor(x => x.AcademicYearId).GreaterThan(0);
        RuleFor(x => x.Semester).IsInEnum();
        RuleFor(x => x.Revision).GreaterThanOrEqualTo(0);
        RuleFor(x => x.SchoolTimeZoneId).NotEmpty().MaximumLength(100).Must(ValidTimeZone)
            .WithMessage("المنطقة الزمنية غير صالحة.");
        RuleFor(x => x).Custom((request, context) =>
        {
            ValidatePeriods(request.DefaultPeriods, "DefaultPeriods", context);
            ScheduleBreakValidation.Validate(request.DefaultBreaks, request.DefaultPeriods, "DefaultBreaks", "DefaultPeriods", context);
            if (request.Days is null || request.Days.Count != 7 ||
                !request.Days.Select(x => x?.Day ?? -1).Order().SequenceEqual(Enumerable.Range(1, 7)))
            {
                context.AddFailure("Days", "يجب تحديد حالة كل يوم من أيام الأسبوع السبعة مرة واحدة.");
                return;
            }
            if (!request.Days.Any(x => x.IsStudyDay))
                context.AddFailure("Days", "حدد يوم دراسة واحداً على الأقل.");
            foreach (var day in request.Days)
            {
                if ((!day.IsStudyDay || day.UsesDefaultBreaks) && day.Breaks?.Count > 0)
                    context.AddFailure($"Days[{day.Day}].Breaks", "يوم العطلة أو اليوم الموروث لا يحتوي على استراحات مستقلة.");
                if (day.IsStudyDay)
                    ScheduleBreakValidation.Validate(day.UsesDefaultBreaks ? request.DefaultBreaks : day.Breaks,
                        day.UsesDefaultSchedule ? request.DefaultPeriods : day.Periods,
                        $"Days[{day.Day}].Breaks", $"Days[{day.Day}].Periods", context);
                if (day.Periods is null)
                    context.AddFailure($"Days[{day.Day}]", "قائمة الحصص مطلوبة.");
                else if (!day.IsStudyDay || day.UsesDefaultSchedule)
                {
                    if (day.Periods.Count != 0)
                        context.AddFailure($"Days[{day.Day}]", "يجب ألا يحتوي يوم العطلة أو اليوم الموروث على حصص مستقلة.");
                }
                else ValidatePeriods(day.Periods, $"Days[{day.Day}].Periods", context);
            }
        });
    }

    private static bool ValidTimeZone(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        try { TimeZoneInfo.FindSystemTimeZoneById(value); return true; }
        catch (TimeZoneNotFoundException) { return false; }
        catch (InvalidTimeZoneException) { return false; }
    }

    private static void ValidatePeriods(IReadOnlyList<BellPeriodDto>? periods, string path,
        ValidationContext<SaveBellScheduleRequest> context)
    {
        if (periods is null || periods.Count == 0 || periods.Any(x => x is null))
        {
            context.AddFailure(path, "أضف حصة واحدة على الأقل ببيانات صحيحة.");
            return;
        }
        var ordered = periods.OrderBy(x => x.Sequence).ToArray();
        if (!ordered.Select(x => x.Sequence).SequenceEqual(Enumerable.Range(1, periods.Count)))
            context.AddFailure(path, "أرقام الحصص يجب أن تبدأ من 1 وتكون متتابعة دون تكرار.");
        for (var i = 0; i < ordered.Length; i++)
        {
            var period = ordered[i];
            if (period.StartLocalTime >= period.EndLocalTime)
                context.AddFailure(path + $"[{period.Sequence}]", $"بداية الحصة {period.Sequence} يجب أن تسبق نهايتها دون عبور منتصف الليل.");
            if (period.DisplayLabel?.Length > 100)
                context.AddFailure(path, "اسم الحصة لا يتجاوز 100 حرف.");
            if (i > 0 && period.StartLocalTime < ordered[i - 1].EndLocalTime)
            {
                var message = $"الحصة {period.Sequence} تبدأ قبل نهاية الحصة {ordered[i - 1].Sequence}.";
                context.AddFailure(path + $"[{period.Sequence}]", message);
                context.AddFailure(path + $"[{ordered[i - 1].Sequence}]", message);
            }
            for (var earlier = 0; earlier < i - 1; earlier++)
            {
                var other = ordered[earlier];
                if (period.StartLocalTime >= other.EndLocalTime || other.StartLocalTime >= period.EndLocalTime) continue;
                var message = $"الحصة {period.Sequence} تتداخل مع الحصة {other.Sequence}.";
                context.AddFailure(path + $"[{period.Sequence}]", message);
                context.AddFailure(path + $"[{other.Sequence}]", message);
            }
        }
    }
}
