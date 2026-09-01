using AlFalah.Application.StudentAffairs.DTOs.Students;
using FluentValidation;

namespace AlFalah.Application.Validators.StudentAffairs;

public sealed class CreateStudentRequestValidator : AbstractValidator<CreateStudentRequestDto>
{
    public CreateStudentRequestValidator()
    {
        RuleFor(request => request.StudentNumber).NotEmpty().MaximumLength(50);
        RuleFor(request => request.IdentityNumber).NotEmpty().MaximumLength(50);
        RuleFor(request => request.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(request => request.MiddleName).MaximumLength(100);
        RuleFor(request => request.LastName).NotEmpty().MaximumLength(100);
        RuleFor(request => request.NationalId).MaximumLength(30);
        RuleFor(request => request.ClassroomId).GreaterThan(0).When(request => request.ClassroomId.HasValue);
        RuleFor(request => request.RollNumber).GreaterThan(0).When(request => request.RollNumber.HasValue);
    }
}

public sealed class UpdateStudentRequestValidator : AbstractValidator<UpdateStudentRequestDto>
{
    public UpdateStudentRequestValidator()
    {
        RuleFor(request => request.StudentNumber).NotEmpty().MaximumLength(50);
        RuleFor(request => request.IdentityNumber).NotEmpty().MaximumLength(50);
        RuleFor(request => request.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(request => request.MiddleName).MaximumLength(100);
        RuleFor(request => request.LastName).NotEmpty().MaximumLength(100);
        RuleFor(request => request.NationalId).MaximumLength(30);
        RuleFor(request => request.ClassroomId).GreaterThan(0).When(request => request.ClassroomId.HasValue);
        RuleFor(request => request.RollNumber).GreaterThan(0).When(request => request.RollNumber.HasValue);
    }
}

public sealed class DeleteStudentRequestValidator : AbstractValidator<DeleteStudentRequestDto>
{
    public DeleteStudentRequestValidator()
    {
        RuleFor(request => request.Reason).NotEmpty().MaximumLength(500);
    }
}
