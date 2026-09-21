using AlFalah.Application.Common;
using AlFalah.Application.Interfaces;
using Microsoft.Extensions.Options;

namespace AlFalah.Infrastructure.Services;

/// <summary>
/// Infrastructure implementation of <see cref="IFeatureFlagService"/> reading from <see cref="IOptions{FeatureFlagsOptions}"/>.
/// </summary>
public sealed class FeatureFlagService : IFeatureFlagService
{
    private readonly IOptions<FeatureFlagsOptions> _options;

    public FeatureFlagService(IOptions<FeatureFlagsOptions> options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public bool IsVisitsV2Enabled(int? schoolId = null) =>
        _options.Value.VisitsV2 ||
        schoolId.HasValue && _options.Value.VisitsV2SchoolIds.Contains(schoolId.Value);
}
