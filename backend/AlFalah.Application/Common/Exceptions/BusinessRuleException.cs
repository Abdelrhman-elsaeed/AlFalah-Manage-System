namespace AlFalah.Application.Common;

/// <summary>
/// A business-rule failure whose message is intentionally safe to return to an API client.
/// Technical exceptions must not use this type; their details remain in server logs only.
/// </summary>
public sealed class BusinessRuleException : Exception
{
    public BusinessRuleException(string message) : base(message) { }
}
