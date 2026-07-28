using AlFalah.Application.DTOs.Users;
using AlFalah.Application.Validators.Users;
using AlFalah.Domain.Enums;
using Xunit;

namespace AlFalah.Tests.Users;

public sealed class UserValidatorTests
{
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
