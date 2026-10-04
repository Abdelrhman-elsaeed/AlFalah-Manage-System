using AlFalah.Application.Storage;
using FluentAssertions;
using Xunit;

namespace AlFalah.Tests.Storage;

public sealed class ReadinessContractTests
{
    [Theory]
    [InlineData(1, 32, 3.13)]
    [InlineData(1, 36, 2.78)]
    [InlineData(2, 36, 5.56)]
    [InlineData(36, 36, 100)]
    public void Rounding_contract_is_two_decimal_places_away_from_zero(int numerator, int denominator, decimal expected) => ReadinessService.Percentage(numerator, denominator).Should().Be(expected);
    [Fact]
    public void Empty_denominator_is_never_reported_as_full_readiness() => ReadinessService.Percentage(0, 0).Should().BeNull();
    [Theory]
    [InlineData(-1, 25, null)]
    [InlineData(1, 101, null)]
    [InlineData(1, 25, "99.1")]
    public void Invalid_pagination_and_standard_codes_are_rejected(int page, int size, string? standard) =>
        FluentActions.Invoking(() => ReadinessService.Validate(new(1, Page: page, PageSize: size, StandardCode: standard))).Should().Throw<ArgumentException>();
}
