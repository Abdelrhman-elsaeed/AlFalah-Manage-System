using AlFalah.Application.DTOs.Users;
using AlFalah.Application.Validators.Users;
using AlFalah.Domain.Enums;
using Xunit;

namespace AlFalah.Tests.Users;

public sealed class UserValidatorTests
{
    [Theory]
    [InlineData(RoleNames.StudentAffairsOfficer)]
    [InlineData(RoleNames.SocialWorker)]
    [InlineData(RoleNames.SecurityGuard)]
    [InlineData(RoleNames.Secretary)]
    [InlineData(RoleNames.Guardian)]
    public void Additional_staff_roles_require_a_school_and_can_be_created_and_updated(string role)
    {
        var request = new UserCreateRequestDto { Username = "staff-member", Password = "Example@Secret2026",
            FirstName = "First", LastName = "Last", Role = role, SchoolId = 1 };
        var validator = new UserCreateRequestValidator();
        Assert.True(validator.Validate(request).IsValid);
        request.SchoolId = null;
        Assert.Contains(validator.Validate(request).Errors, x => x.PropertyName == nameof(request.SchoolId));
        Assert.True(new UserUpdateRequestValidator().Validate(new UserUpdateRequestDto {
            FirstName = "First", LastName = "Last", Role = role, SchoolId = 1
        }).IsValid);
    }

    [Fact]
    public void Instructor_create_requires_its_own_password()
    {
        var request = new UserCreateRequestDto
        {
            Username = "instructor-test",
            Role = RoleNames.Instructor,
            FullName = "معلم تجريبي",
            EmployeeNumber = "1234",
            Subject = "الرياضيات",
            Stage = SchoolStage.Primary,
            SchoolId = 1
        };

        var result = new UserCreateRequestValidator().Validate(request);

        Assert.Contains(result.Errors, error => error.PropertyName == nameof(UserCreateRequestDto.Password));
    }

    [Fact]
    public void Instructor_create_accepts_a_supplied_password()
    {
        var request = new UserCreateRequestDto
        {
            Username = "instructor-test",
            Role = RoleNames.Instructor,
            Password = "Passw0rd!",
            FullName = "معلم تجريبي",
            EmployeeNumber = "1234",
            Subject = "الرياضيات",
            Stage = SchoolStage.Primary,
            SchoolId = 1
        };

        var result = new UserCreateRequestValidator().Validate(request);

        Assert.DoesNotContain(result.Errors, error => error.PropertyName == nameof(UserCreateRequestDto.Password));
    }

    [Fact]
    public void Non_instructor_create_still_requires_a_password()
    {
        var request = new UserCreateRequestDto
        {
            Username = "moderator-test",
            Role = RoleNames.Moderator,
            Password = ""
        };

        var result = new UserCreateRequestValidator().Validate(request);

        Assert.Contains(result.Errors, error => error.PropertyName == nameof(UserCreateRequestDto.Password));
    }
}
