using AlFalah.Application.IntelligentTimetable.DTOs;
using FluentValidation;

namespace AlFalah.Application.Validators.IntelligentTimetable;

public sealed class SaveSubjectValidator : AbstractValidator<SaveSubjectRequest>
{
    public SaveSubjectValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200).WithMessage("اسم المادة مطلوب وبحد أقصى 200 حرف.");
        RuleFor(x => x.Color).Matches("^#[0-9a-fA-F]{6}$").WithMessage("اختر لوناً صالحاً للمادة.");
        RuleFor(x => x.Revision).GreaterThanOrEqualTo(0);
    }
}
public sealed class SubjectRulesValidator : AbstractValidator<SubjectRulesRequest>
{
    public SubjectRulesValidator()
    {
        RuleFor(x => x.IndividualPeriodCount).InclusiveBetween(0, 100);
        RuleFor(x => x.PairedBlockCount).InclusiveBetween(0, 50);
        RuleFor(x => x).Must(x => (long)x.IndividualPeriodCount + 2L * x.PairedBlockCount > 0).WithMessage("إجمالي الحصص الأسبوعية يجب أن يكون أكبر من صفر.");
        RuleFor(x => x.TimePreference).Must(x => x is "None" or "Early" or "Late");
        When(x => x.TimePreference != "None", () => {
            RuleFor(x => x.EarliestPeriodSequence).NotNull().InclusiveBetween(1, 100);
            RuleFor(x => x.LatestPreferredPeriodSequence).NotNull().InclusiveBetween(1, 100);
            RuleFor(x => x).Must(x => x.LatestPreferredPeriodSequence >= x.EarliestPeriodSequence).WithMessage("نهاية نافذة التفضيل يجب أن تلي بدايتها.");
        });
        RuleFor(x => x.AllowedDays).NotNull().Must(x => x is not null && x.Count <= 7 && x.Distinct().Count() == x.Count && x.All(d => d is >= 1 and <= 7));
        RuleFor(x => x.RoomIds).NotNull().Must(x => x is not null && x.Count <= 100 && x.Distinct().Count() == x.Count && x.All(id => id > 0));
        RuleFor(x => x).Must(x => !x.PreferredRoomId.HasValue || x.RoomIds?.Contains(x.PreferredRoomId.Value) == true).WithMessage("الغرفة المفضلة يجب أن تكون ضمن الغرف المحددة.");
        RuleFor(x => x.FixedSlots).NotNull().Must(x => x is not null && x.Count <= 100 && x.Distinct().Count() == x.Count && x.All(s => s is not null && s.Day is >= 1 and <= 7 && s.Period > 0));
    }
}
public sealed class AllocateSubjectValidator : AbstractValidator<AllocateSubjectRequest>
{
    public AllocateSubjectValidator()
    {
        RuleFor(x => x.SubjectId).GreaterThan(0);
        RuleFor(x => x.Classes).NotEmpty().Must(x => x is not null && x.Count <= 500 && x.All(c => c is not null && c.ClassroomId > 0 && c.Revision >= 0) && x.Select(c => c.ClassroomId).Distinct().Count() == x.Count);
        RuleFor(x => x.Rules).NotNull().SetValidator(new SubjectRulesValidator());
    }
}
