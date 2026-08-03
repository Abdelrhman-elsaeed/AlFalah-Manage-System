using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AlFalah.Application.DTOs.StudentAnalyzer;
using AlFalah.Application.Interfaces;
using AlFalah.Domain.Enums;

namespace AlFalah.Infrastructure.Services;

/// <summary>Outbound HTTP adapter for Groq, Gemini, and OpenRouter.</summary>
public sealed class StudentAnalyzerAiClient : IStudentAnalyzerAiClient
{
    private const string GroqBase = "https://api.groq.com/openai/v1";
    private const string GeminiBase = "https://generativelanguage.googleapis.com/v1beta";
    private const string OpenRouterBase = "https://openrouter.ai/api/v1";
    private readonly IHttpClientFactory _httpClientFactory;

    public StudentAnalyzerAiClient(IHttpClientFactory httpClientFactory) =>
        _httpClientFactory = httpClientFactory;

    public Task<StudentAnalyzerAiResponse> AnalyzeAsync(
        StudentAnalyzerAiRequest request,
        CancellationToken cancellationToken = default) => request.Provider switch
    {
        StudentAnalyzerProvider.Groq => AnalyzeOpenAiCompatibleAsync(
            request, $"{GroqBase}/chat/completions", includeOpenRouterHeaders: false, cancellationToken),
        StudentAnalyzerProvider.OpenRouter => AnalyzeOpenAiCompatibleAsync(
            request, $"{OpenRouterBase}/chat/completions", includeOpenRouterHeaders: true, cancellationToken),
        StudentAnalyzerProvider.Gemini => AnalyzeGeminiAsync(request, cancellationToken),
        _ => throw new ArgumentException("مزود الذكاء الاصطناعي غير مدعوم.")
    };

    public Task<IReadOnlyList<StudentAnalyzerModelDto>> GetModelsAsync(
        StudentAnalyzerProvider provider,
        string apiKey,
        CancellationToken cancellationToken = default) => provider switch
    {
        StudentAnalyzerProvider.Groq => GetGroqModelsAsync(apiKey, cancellationToken),
        StudentAnalyzerProvider.Gemini => GetGeminiModelsAsync(apiKey, cancellationToken),
        StudentAnalyzerProvider.OpenRouter => GetOpenRouterModelsAsync(apiKey, cancellationToken),
        _ => throw new ArgumentException("مزود الذكاء الاصطناعي غير مدعوم.")
    };

    private async Task<StudentAnalyzerAiResponse> AnalyzeOpenAiCompatibleAsync(
        StudentAnalyzerAiRequest request,
        string url,
        bool includeOpenRouterHeaders,
        CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, url);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", request.ApiKey);
        if (includeOpenRouterHeaders)
        {
            message.Headers.TryAddWithoutValidation("HTTP-Referer", "https://alfalah-schools.local");
            message.Headers.TryAddWithoutValidation("X-OpenRouter-Title", "AlFalah Student Analyzer");
        }

        message.Content = JsonContent.Create(new
        {
            model = request.Model,
            messages = new object[]
            {
                new { role = "system", content = request.SystemPrompt },
                new { role = "user", content = request.UserPrompt }
            },
            temperature = 0.7,
            max_tokens = 4096,
            top_p = 0.95
        });

        using var response = await SendAsync(message, request.Provider, cancellationToken);
        await EnsureSuccessAsync(response, request.Provider, cancellationToken);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken));
        var text = TryGetString(json.RootElement, "choices", 0, "message", "content");
        if (string.IsNullOrWhiteSpace(text))
            throw new InvalidOperationException("لم يتم الحصول على استجابة تحليل من مزود الذكاء الاصطناعي. حاول مرة أخرى.");

        var actualModel = json.RootElement.TryGetProperty("model", out var model) && model.ValueKind == JsonValueKind.String
            ? model.GetString() ?? request.Model
            : request.Model;
        return new StudentAnalyzerAiResponse(text, actualModel);
    }

    private async Task<StudentAnalyzerAiResponse> AnalyzeGeminiAsync(
        StudentAnalyzerAiRequest request,
        CancellationToken cancellationToken)
    {
        var model = request.Model.StartsWith("models/", StringComparison.OrdinalIgnoreCase)
            ? request.Model["models/".Length..]
            : request.Model;
        using var message = new HttpRequestMessage(
            HttpMethod.Post,
            $"{GeminiBase}/models/{Uri.EscapeDataString(model)}:generateContent");
        message.Headers.TryAddWithoutValidation("x-goog-api-key", request.ApiKey);
        message.Content = JsonContent.Create(new
        {
            contents = new[] { new { parts = new[] { new { text = request.UserPrompt } } } },
            generationConfig = new
            {
                temperature = 0.7,
                topK = 40,
                topP = 0.95,
                maxOutputTokens = 4096
            },
            safetySettings = new object[]
            {
                new { category = "HARM_CATEGORY_HARASSMENT", threshold = "BLOCK_NONE" },
                new { category = "HARM_CATEGORY_HATE_SPEECH", threshold = "BLOCK_NONE" },
                new { category = "HARM_CATEGORY_SEXUALLY_EXPLICIT", threshold = "BLOCK_NONE" },
                new { category = "HARM_CATEGORY_DANGEROUS_CONTENT", threshold = "BLOCK_NONE" }
            }
        });

        using var response = await SendAsync(message, request.Provider, cancellationToken);
        await EnsureSuccessAsync(response, request.Provider, cancellationToken);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken));
        var text = TryGetString(json.RootElement, "candidates", 0, "content", "parts", 0, "text");
        if (string.IsNullOrWhiteSpace(text))
            throw new InvalidOperationException("لم يتم الحصول على استجابة تحليل من Gemini. حاول مرة أخرى.");
        return new StudentAnalyzerAiResponse(text, model);
    }

    private async Task<IReadOnlyList<StudentAnalyzerModelDto>> GetGroqModelsAsync(
        string apiKey,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{GroqBase}/models");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        using var response = await SendAsync(request, StudentAnalyzerProvider.Groq, cancellationToken);
        await EnsureSuccessAsync(response, StudentAnalyzerProvider.Groq, cancellationToken);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken));
        if (!json.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
            return Array.Empty<StudentAnalyzerModelDto>();

        return data.EnumerateArray()
            .Where(x => !x.TryGetProperty("active", out var active) || active.ValueKind != JsonValueKind.False)
            .Select(x => new StudentAnalyzerModelDto(
                x.GetProperty("id").GetString() ?? string.Empty,
                x.GetProperty("id").GetString() ?? string.Empty,
                null,
                TryGetInt(x, "context_window"),
                true))
            .Where(x => x.Id.Length > 0)
            .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task<IReadOnlyList<StudentAnalyzerModelDto>> GetGeminiModelsAsync(
        string apiKey,
        CancellationToken cancellationToken)
    {
        var models = new List<StudentAnalyzerModelDto>();
        string? pageToken = null;
        do
        {
            var url = $"{GeminiBase}/models?pageSize=1000";
            if (!string.IsNullOrWhiteSpace(pageToken))
                url += $"&pageToken={Uri.EscapeDataString(pageToken)}";
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.TryAddWithoutValidation("x-goog-api-key", apiKey);
            using var response = await SendAsync(request, StudentAnalyzerProvider.Gemini, cancellationToken);
            await EnsureSuccessAsync(response, StudentAnalyzerProvider.Gemini, cancellationToken);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken));
            if (json.RootElement.TryGetProperty("models", out var items) && items.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in items.EnumerateArray())
                {
                    if (!SupportsGenerateContent(item)) continue;
                    var rawName = item.TryGetProperty("name", out var nameElement) ? nameElement.GetString() : null;
                    var id = rawName?.StartsWith("models/", StringComparison.OrdinalIgnoreCase) == true
                        ? rawName["models/".Length..]
                        : rawName;
                    if (string.IsNullOrWhiteSpace(id)) continue;
                    models.Add(new StudentAnalyzerModelDto(
                        id,
                        GetString(item, "displayName") ?? id,
                        GetString(item, "description"),
                        TryGetInt(item, "inputTokenLimit"),
                        true));
                }
            }
            pageToken = GetString(json.RootElement, "nextPageToken");
        } while (!string.IsNullOrWhiteSpace(pageToken));

        return models
            .GroupBy(x => x.Id, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.First())
            .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task<IReadOnlyList<StudentAnalyzerModelDto>> GetOpenRouterModelsAsync(
        string apiKey,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{OpenRouterBase}/models?output_modalities=text");
        if (!string.IsNullOrWhiteSpace(apiKey))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        using var response = await SendAsync(request, StudentAnalyzerProvider.OpenRouter, cancellationToken);
        await EnsureSuccessAsync(response, StudentAnalyzerProvider.OpenRouter, cancellationToken);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken));
        if (!json.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
            return Array.Empty<StudentAnalyzerModelDto>();

        return data.EnumerateArray()
            .Where(IsOpenRouterModelFree)
            .Select(x => new StudentAnalyzerModelDto(
                GetString(x, "id") ?? string.Empty,
                GetString(x, "name") ?? GetString(x, "id") ?? string.Empty,
                GetString(x, "description"),
                TryGetInt(x, "context_length"),
                IsOpenRouterModelFree(x)))
            .Where(x => x.Id.Length > 0)
            .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        StudentAnalyzerProvider provider,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _httpClientFactory.CreateClient("StudentAnalyzerAi")
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new InvalidOperationException($"انتهت مهلة الاتصال مع {ProviderLabel(provider)}. حاول مرة أخرى.");
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException($"تعذر الاتصال مع {ProviderLabel(provider)}. تأكد من اتصال الخادم بالإنترنت.", ex);
        }
    }

    private static async Task EnsureSuccessAsync(
        HttpResponseMessage response,
        StudentAnalyzerProvider provider,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode) return;
        var providerMessage = await ExtractErrorMessageAsync(response, cancellationToken);
        var label = ProviderLabel(provider);
        var message = response.StatusCode switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => $"مفتاح {label} غير صحيح أو لا يملك الصلاحية المطلوبة.",
            HttpStatusCode.TooManyRequests => $"تم تجاوز حد الاستخدام لدى {label}. حاول لاحقًا أو استخدم مفتاحًا آخر.",
            HttpStatusCode.NotFound => $"الموديل المحدد غير متاح حاليًا لدى {label}.",
            HttpStatusCode.PaymentRequired => $"حساب {label} يحتاج رصيدًا أو أن الموديل المحدد لم يعد مجانيًا.",
            _ => $"رفض {label} الطلب ({(int)response.StatusCode})."
        };
        if (!string.IsNullOrWhiteSpace(providerMessage)) message += $" {providerMessage}";
        throw new InvalidOperationException(message);
    }

    private static async Task<string?> ExtractErrorMessageAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            using var json = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken));
            if (json.RootElement.TryGetProperty("error", out var error))
            {
                if (error.ValueKind == JsonValueKind.Object && error.TryGetProperty("message", out var message))
                    return message.GetString();
                if (error.ValueKind == JsonValueKind.String) return error.GetString();
            }
        }
        catch (JsonException) { }
        return null;
    }

    private static bool SupportsGenerateContent(JsonElement model)
    {
        if (!model.TryGetProperty("supportedGenerationMethods", out var methods) || methods.ValueKind != JsonValueKind.Array)
            return false;
        return methods.EnumerateArray().Any(x =>
            string.Equals(x.GetString(), "generateContent", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsOpenRouterModelFree(JsonElement model)
    {
        if (!model.TryGetProperty("pricing", out var pricing) || pricing.ValueKind != JsonValueKind.Object)
            return false;
        return IsZeroPrice(pricing, "prompt")
            && IsZeroPrice(pricing, "completion")
            && IsZeroPrice(pricing, "request");
    }

    private static bool IsZeroPrice(JsonElement pricing, string property)
    {
        if (!pricing.TryGetProperty(property, out var value)) return true;
        var raw = value.ValueKind == JsonValueKind.String ? value.GetString() : value.GetRawText();
        return decimal.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) && parsed == 0m;
    }

    private static int? TryGetInt(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value)) return null;
        if (value.TryGetInt32(out var result)) return result;
        return value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out result)
            ? result
            : null;
    }

    private static string? GetString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static string? TryGetString(JsonElement root, params object[] path)
    {
        var current = root;
        foreach (var segment in path)
        {
            if (segment is string property)
            {
                if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(property, out current)) return null;
            }
            else if (segment is int index)
            {
                if (current.ValueKind != JsonValueKind.Array || current.GetArrayLength() <= index) return null;
                current = current[index];
            }
        }
        return current.ValueKind == JsonValueKind.String ? current.GetString() : null;
    }

    private static string ProviderLabel(StudentAnalyzerProvider provider) => provider switch
    {
        StudentAnalyzerProvider.Groq => "Groq",
        StudentAnalyzerProvider.Gemini => "Gemini",
        StudentAnalyzerProvider.OpenRouter => "OpenRouter",
        _ => "مزود الذكاء الاصطناعي"
    };
}
