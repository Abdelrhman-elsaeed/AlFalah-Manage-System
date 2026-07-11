using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace AlFalah.Application.Common;

/// <summary>
/// Helper for running FluentValidation validators explicitly. Keeps controllers thin
/// while returning the standard ApiResponse&lt;T&gt; envelope on validation failure.
/// </summary>
public static class ValidationHelper
{
    public static async Task<List<string>> ValidateAsync<T>(
        IServiceProvider serviceProvider,
        T instance,
        CancellationToken cancellationToken = default)
    {
        var validator = serviceProvider.GetService<IValidator<T>>();
        if (validator == null) return new List<string>();

        var result = await validator.ValidateAsync(instance, cancellationToken);
        return result.IsValid
            ? new List<string>()
            : result.Errors.Select(e => e.ErrorMessage).ToList();
    }
}