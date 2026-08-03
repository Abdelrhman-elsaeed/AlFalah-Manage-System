using AlFalah.Domain.Enums;

namespace AlFalah.Domain.Entities;

/// <summary>
/// One encrypted, school-owned set of AI-provider credentials. API keys are
/// protected at rest and are never returned from an API response.
/// </summary>
public sealed class SchoolStudentAnalyzerSettings
{
    public int Id { get; set; }
    public int SchoolId { get; set; }
    public StudentAnalyzerProvider ActiveProvider { get; set; } = StudentAnalyzerProvider.Groq;

    public string? ProtectedGroqApiKey { get; set; }
    public string GroqModel { get; set; } = "llama-3.3-70b-versatile";
    public string? ProtectedGeminiApiKey { get; set; }
    public string GeminiModel { get; set; } = "gemini-2.5-flash";
    public string? ProtectedOpenRouterApiKey { get; set; }
    public string OpenRouterModel { get; set; } = "openrouter/free";

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string UpdatedByUserId { get; set; } = string.Empty;

    public School School { get; set; } = null!;
    public ApplicationUser UpdatedByUser { get; set; } = null!;
}
