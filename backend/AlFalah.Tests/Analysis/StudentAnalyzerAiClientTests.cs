using System.Net;
using System.Text;
using System.Text.Json;
using AlFalah.Application.DTOs.StudentAnalyzer;
using AlFalah.Domain.Enums;
using AlFalah.Infrastructure.Services;
using FluentAssertions;
using Xunit;

namespace AlFalah.Tests.Analysis;

public sealed class StudentAnalyzerAiClientTests
{
    [Fact]
    public async Task Groq_request_preserves_prototype_messages_and_generation_parameters()
    {
        var handler = new CaptureHandler("""{"model":"llama-test","choices":[{"message":{"content":"result"}}]}""");
        var client = new StudentAnalyzerAiClient(new Factory(handler));

        var result = await client.AnalyzeAsync(new(
            StudentAnalyzerProvider.Groq,
            "secret-key",
            "llama-test",
            "system prompt",
            "user prompt"));

        result.Text.Should().Be("result");
        handler.Uri.Should().Be("https://api.groq.com/openai/v1/chat/completions");
        handler.Authorization.Should().Be("Bearer secret-key");
        using var json = JsonDocument.Parse(handler.Body!);
        var root = json.RootElement;
        root.GetProperty("temperature").GetDecimal().Should().Be(0.7m);
        root.GetProperty("max_tokens").GetInt32().Should().Be(4096);
        root.GetProperty("top_p").GetDecimal().Should().Be(0.95m);
        root.GetProperty("messages")[0].GetProperty("role").GetString().Should().Be("system");
        root.GetProperty("messages")[0].GetProperty("content").GetString().Should().Be("system prompt");
        root.GetProperty("messages")[1].GetProperty("content").GetString().Should().Be("user prompt");
    }

    [Fact]
    public async Task Gemini_request_preserves_prototype_generation_and_safety_settings()
    {
        var handler = new CaptureHandler("""{"candidates":[{"content":{"parts":[{"text":"result"}]}}]}""");
        var client = new StudentAnalyzerAiClient(new Factory(handler));

        await client.AnalyzeAsync(new(
            StudentAnalyzerProvider.Gemini,
            "gemini-key",
            "gemini-test",
            "unused for prototype Gemini flow",
            "user prompt"));

        handler.Uri.Should().Be("https://generativelanguage.googleapis.com/v1beta/models/gemini-test:generateContent");
        handler.GoogleApiKey.Should().Be("gemini-key");
        using var json = JsonDocument.Parse(handler.Body!);
        var root = json.RootElement;
        var generation = root.GetProperty("generationConfig");
        generation.GetProperty("temperature").GetDecimal().Should().Be(0.7m);
        generation.GetProperty("topK").GetInt32().Should().Be(40);
        generation.GetProperty("topP").GetDecimal().Should().Be(0.95m);
        generation.GetProperty("maxOutputTokens").GetInt32().Should().Be(4096);
        root.GetProperty("safetySettings").GetArrayLength().Should().Be(4);
        root.GetProperty("safetySettings").EnumerateArray()
            .Should().OnlyContain(item => item.GetProperty("threshold").GetString() == "BLOCK_NONE");
    }

    [Fact]
    public async Task OpenRouter_models_include_only_free_choices()
    {
        var handler = new CaptureHandler("""
            {"data":[
              {"id":"openrouter/free","name":"Free Models Router","context_length":200000,"pricing":{"prompt":"0","completion":"0","request":"0"}},
              {"id":"example/specific:free","name":"Specific Free Model","context_length":128000,"pricing":{"prompt":"0","completion":"0"}},
              {"id":"example/paid","name":"Paid Model","context_length":64000,"pricing":{"prompt":"0.000001","completion":"0.000002","request":"0"}}
            ]}
            """);
        var client = new StudentAnalyzerAiClient(new Factory(handler));

        var models = await client.GetModelsAsync(StudentAnalyzerProvider.OpenRouter, "openrouter-key");

        handler.Uri.Should().Be("https://openrouter.ai/api/v1/models?output_modalities=text");
        handler.Authorization.Should().Be("Bearer openrouter-key");
        models.Select(model => model.Id).Should().BeEquivalentTo(
            "openrouter/free", "example/specific:free");
        models.Single(model => model.Id == "openrouter/free").IsFree.Should().BeTrue();
        models.Single(model => model.Id == "example/specific:free").IsFree.Should().BeTrue();
        models.Should().NotContain(model => model.Id == "example/paid");
    }

    private sealed class Factory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class CaptureHandler(string responseJson) : HttpMessageHandler
    {
        public string? Uri { get; private set; }
        public string? Body { get; private set; }
        public string? Authorization { get; private set; }
        public string? GoogleApiKey { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Uri = request.RequestUri?.ToString();
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            Authorization = request.Headers.Authorization?.ToString();
            GoogleApiKey = request.Headers.TryGetValues("x-goog-api-key", out var values) ? values.Single() : null;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            };
        }
    }
}
