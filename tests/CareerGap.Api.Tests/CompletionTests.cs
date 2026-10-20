using System.Net;
using System.Text;
using System.Text.Json;
using CareerGap.Api.Ai;
using CareerGap.Api.Files;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace CareerGap.Api.Tests;

public sealed class CompletionTests
{
    [Theory]
    [InlineData("length", "{\"fields\":[]}", "ai_output_truncated", false)]
    [InlineData("tool_calls", "{\"fields\":[]}", "ai_completion_invalid", false)]
    [InlineData(null, "{\"fields\":[]}", "ai_completion_invalid", false)]
    [InlineData("stop", " ", "ai_output_empty", true)]
    public async Task AdapterKeepsFinishReasonAndUsageEvenWhenCompletionMustNotBePublished(string? finish, string content, string code, bool retry)
    {
        var response = JsonSerializer.Serialize(new { choices = new[] { new { finish_reason = finish, message = new { content } } },
            usage = new { prompt_tokens = 100, completion_tokens = 25 } });
        var handler = new SyntheticHandler(response);
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://synthetic.invalid/") };
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Ai:ApiKey"] = "synthetic-test-not-a-secret" }).Build();
        var client = new DeepSeekClient(http, config);
        var parameters = new MethodParameters("synthetic-model", 16000, 2048, 0.3m, 1.2m, "synthetic", FullCvExtraction.Contract);
        var reply = await client.ExtractAsync(new("application/pdf", [new("p1", "Synthetic React")]), parameters, default);
        Assert.Equal(100L, reply.InputTokens); Assert.Equal(25L, reply.OutputTokens); Assert.Equal(finish, reply.FinishReason);
        var failure = Assert.Throws<ProviderError>(() => CompletionValidator.Validate(reply));
        Assert.Equal(code, failure.Code); Assert.Equal(retry, failure.Retryable);
        Assert.Equal(1, handler.Calls);
        using var payload = JsonDocument.Parse(handler.Request!);
        Assert.Equal(2048, payload.RootElement.GetProperty("max_tokens").GetInt32());
        Assert.Contains("experience[0..9]", payload.RootElement.GetProperty("messages")[0].GetProperty("content").GetString());
    }

    [Fact]
    public void FullCvOutputBudgetMustBeExplicitAndSnapshotDoesNotReuseSkillsLimit()
    {
        var options = new AiOptions { MaxOutputTokens = 512 };
        Assert.True(options.SupportsContract("skills-v1")); Assert.False(options.SupportsContract(FullCvExtraction.Contract));
        Assert.Throws<JobError>(() => options.Snapshot(FullCvExtraction.Contract));
        options.FullCvMaxOutputTokens = 2048;
        Assert.True(options.SupportsContract(FullCvExtraction.Contract));
        Assert.Equal(512, options.Snapshot().MaxOutputTokens);
        Assert.Equal(2048, options.Snapshot(FullCvExtraction.Contract).MaxOutputTokens);
        options.FullCvMaxOutputTokens = 2049; Assert.False(options.SupportsContract(FullCvExtraction.Contract));
    }

    private sealed class SyntheticHandler(string response) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public string? Request { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++; Request = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new(HttpStatusCode.OK) { Content = new StringContent(response, Encoding.UTF8, "application/json") };
        }
    }
}
