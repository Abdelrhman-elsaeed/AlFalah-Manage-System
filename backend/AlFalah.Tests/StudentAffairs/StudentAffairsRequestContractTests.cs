using AlFalah.Application.StudentAffairs;
using FluentAssertions;
using MediatR;
using Xunit;

namespace AlFalah.Tests.StudentAffairs;

public sealed class StudentAffairsRequestContractTests
{
    [Fact]
    public void Every_student_affairs_request_has_a_handler_or_an_explicit_deferred_workstream()
    {
        var assembly = typeof(StudentAffairsAssemblyMarker).Assembly;
        var requestTypes = assembly.GetTypes()
            .Where(type => !type.IsAbstract
                && type.Namespace?.StartsWith("AlFalah.Application.StudentAffairs", StringComparison.Ordinal) == true)
            .Select(type => new
            {
                Request = type,
                Contract = type.GetInterfaces().SingleOrDefault(contract =>
                    contract.IsGenericType && contract.GetGenericTypeDefinition() == typeof(IRequest<>))
            })
            .Where(item => item.Contract is not null)
            .ToArray();

        var unsafeRequests = requestTypes
            .Where(item =>
            {
                var response = item.Contract!.GetGenericArguments()[0];
                var handlerContract = typeof(IRequestHandler<,>).MakeGenericType(item.Request, response);
                var hasHandler = assembly.GetTypes().Any(type => !type.IsAbstract && handlerContract.IsAssignableFrom(type));
                return !hasHandler && !DeferredStudentAffairsRequests.Workstreams.ContainsKey(item.Request);
            })
            .Select(item => item.Request.FullName)
            .ToArray();

        unsafeRequests.Should().BeEmpty(
            "a public request without a handler must be explicitly contained and assigned to a later workstream");
        DeferredStudentAffairsRequests.Workstreams.Should().HaveCount(15,
            "W5 operational read contracts now have executable, officer-scoped handlers");
    }
}
