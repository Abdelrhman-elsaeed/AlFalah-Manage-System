using AlFalah.Application.StudentAffairs.DTOs.Classrooms;
using FluentValidation;

namespace AlFalah.Application.Validators.StudentAffairs;

public sealed class CreateClassroomRequestValidator : AbstractValidator<CreateClassroomRequestDto>
{
    public CreateClassroomRequestValidator()
    {
        RuleFor(request => request.AcademicYearId).GreaterThan(0);
        RuleFor(request => request.Stage).IsInEnum();
        RuleFor(request => request.GradeLevel).InclusiveBetween((byte)1, (byte)12);
        RuleFor(request => request.Section).NotEmpty().MaximumLength(50);
        RuleFor(request => request.ClassLabel).NotEmpty().MaximumLength(50);
    }
}

public sealed class UpdateClassroomRequestValidator : AbstractValidator<UpdateClassroomRequestDto>
{
    public UpdateClassroomRequestValidator()
    {
        RuleFor(request => request.Section).NotEmpty().MaximumLength(50);
        RuleFor(request => request.ClassLabel).NotEmpty().MaximumLength(50);
    }
}

public sealed class DeleteClassroomRequestValidator : AbstractValidator<DeleteClassroomRequestDto>
{
    public DeleteClassroomRequestValidator()
    {
        RuleFor(request => request.Reason).NotEmpty().MaximumLength(500);
    }
}
