using AlFalah.Application.DTOs.StudentAnalyzer;
using FluentValidation;

namespace AlFalah.Application.Validators.StudentAnalyzer;

public sealed class UpdateStudentAnalyzerGrantsValidator : AbstractValidator<UpdateStudentAnalyzerGrantsRequest>
{
    public UpdateStudentAnalyzerGrantsValidator()
    {
        RuleFor(x => x.UserIds).NotNull().Must(x => x.Count <= 100)
            .WithMessage("لا يمكن تفويض أكثر من 100 مستخدم دفعة واحدة.");
        RuleForEach(x => x.UserIds).NotEmpty().MaximumLength(450);
    }
}

public sealed class UpdateStudentAnalyzerSettingsValidator : AbstractValidator<UpdateStudentAnalyzerSettingsRequest>
{
    public UpdateStudentAnalyzerSettingsValidator()
    {
        RuleFor(x => x.ActiveProvider).IsInEnum();
        RuleFor(x => x.GroqApiKey).MaximumLength(4000);
        RuleFor(x => x.GeminiApiKey).MaximumLength(4000);
        RuleFor(x => x.OpenRouterApiKey).MaximumLength(4000);
        RuleFor(x => x.GroqModel).MaximumLength(200);
        RuleFor(x => x.GeminiModel).MaximumLength(200);
        RuleFor(x => x.OpenRouterModel).MaximumLength(300);
    }
}

public sealed class AnalyzeStudentRequestValidator : AbstractValidator<AnalyzeStudentRequest>
{
    public AnalyzeStudentRequestValidator()
    {
        RuleFor(x => x.SourceFileId).GreaterThan(0);
        RuleFor(x => x.StudentName).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Grants).NotNull().Must(x => x.Count <= 500);
        RuleFor(x => x.Deductions).NotNull().Must(x => x.Count <= 500);
        RuleForEach(x => x.Grants).SetValidator(new StudentAnalyzerDataPointValidator());
        RuleForEach(x => x.Deductions).SetValidator(new StudentAnalyzerDataPointValidator());
    }
}

public sealed class StudentAnalyzerDataPointValidator : AbstractValidator<StudentAnalyzerDataPointDto>
{
    public StudentAnalyzerDataPointValidator()
    {
        RuleFor(x => x.Column).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Value).NotNull().MaximumLength(2000);
    }
}
