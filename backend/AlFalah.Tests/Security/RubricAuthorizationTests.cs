using AlFalah.Api.Controllers;
using AlFalah.Domain.Enums;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace AlFalah.Tests.Security;

public sealed class RubricAuthorizationTests
{
    [Fact]
    public void Rubric_Controller_Is_Restricted_To_MainManager_And_SuperAdmin()
    {
        var authorize = typeof(RubricController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Single();

        authorize.Roles.Should().Be(RoleNames.SuperAdmin + "," + RoleNames.MainManager);
    }
}
