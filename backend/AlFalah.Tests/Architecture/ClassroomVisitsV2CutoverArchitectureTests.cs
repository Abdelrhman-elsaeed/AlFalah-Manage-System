using AlFalah.Api.Controllers;
using AlFalah.Application.Interfaces;
using AlFalah.Infrastructure.Services;
using FluentAssertions;
using Xunit;

namespace AlFalah.Tests.Architecture;

public sealed class ClassroomVisitsV2CutoverArchitectureTests
{
    [Fact]
    public void V2_runtime_types_must_not_depend_on_the_legacy_visit_service()
    {
        var forbidden = new[] { typeof(IVisitService), typeof(VisitService) };
        var runtimeTypes = new[]
        {
            typeof(VisitsV2Controller),
            typeof(VisitV2Service),
            typeof(ComplaintService),
            typeof(VisitWorkflowDispatcher)
        };

        var violations = runtimeTypes
            .SelectMany(type => type.GetConstructors()
                .SelectMany(constructor => constructor.GetParameters()
                    .Where(parameter => forbidden.Contains(parameter.ParameterType))
                    .Select(parameter => $"{type.FullName} -> {parameter.ParameterType.FullName}")))
            .ToArray();

        violations.Should().BeEmpty("V2 must be independently wired from the legacy workflow");
    }

    [Theory]
    [InlineData("ApproveAsync")]
    [InlineData("RejectAsync")]
    [InlineData("ReopenAsync")]
    public void V2_service_contract_must_own_its_workflow_transitions(string methodName)
    {
        typeof(IVisitV2Service).GetMethod(methodName).Should().NotBeNull();
    }

    [Fact]
    public void Complaint_reopen_uses_the_neutral_version_aware_dispatcher()
    {
        var dependency = typeof(ComplaintService).GetConstructors().Single().GetParameters()
            .SingleOrDefault(parameter => parameter.ParameterType == typeof(IVisitWorkflowDispatcher));

        dependency.Should().NotBeNull();
        typeof(VisitWorkflowDispatcher).GetConstructors().Single().GetParameters()
            .Should().ContainSingle(parameter => parameter.ParameterType == typeof(IVisitV2Service));
    }
}
