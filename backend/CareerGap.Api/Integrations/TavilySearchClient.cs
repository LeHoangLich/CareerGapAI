using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace CareerGap.Api.Integrations;

public sealed class SearchOptions
{
    public string Provider { get; set; } = "Fake";
    public int CreditLimit { get; set; }
    public int UserCreditLimit { get; set; } = 10;
    public string PeriodStart { get; set; } = "";
    public string PeriodEnd { get; set; } = "";
    public string CreditsVerifiedAt { get; set; } = "";
    public int MetadataCacheMinutes { get; set; }
    public bool CacheTermsVerified { get; set; }
    public bool Ready(DateTimeOffset now) => Provider == "Tavily" && CreditLimit > 0 && UserCreditLimit > 0 &&
        DateTimeOffset.TryParse(PeriodStart, out var start) && DateTimeOffset.TryParse(PeriodEnd, out var end) && start <= now && end > now && end - start <= TimeSpan.FromDays(32) &&
        DateTimeOffset.TryParse(CreditsVerifiedAt, out var verified) && verified <= now && verified > now.AddDays(-31);
}
public sealed class IntegrationError(string code) : Exception(code) { public string Code { get; } = code; }
public static class IntegrationHttp
{
    public static async Task<byte[]> ReadLimited(HttpResponseMessage response, CancellationToken ct)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(ct); using var memory = new MemoryStream(); var buffer = new byte[4096];
        while (true) { var count = await stream.ReadAsync(buffer, ct); if (count == 0) break; if (memory.Length + count > 131072) throw new IntegrationError("provider_output_limit"); memory.Write(buffer, 0, count); }
        return memory.ToArray();
    }
    public static bool SafePublicLink(string value, out Uri? uri)
    {
        uri = null;
        if (value.Length > 2048 || !Uri.TryCreate(value, UriKind.Absolute, out var parsed) || parsed.Scheme != "https" || !string.IsNullOrEmpty(parsed.UserInfo) || parsed.Port != 443 || parsed.IsLoopback) return false;
        var host = parsed.IdnHost;
        if (!host.Contains('.') || host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase) || host.EndsWith(".local", StringComparison.OrdinalIgnoreCase) || IPAddress.TryParse(host, out _)) return false;
        uri = parsed; return true;
    }
}
// Optional proposed adapter; the application remains Fake until owner-provided configuration/limits exist.
public sealed class TavilySearchClient(HttpClient http, IConfiguration config) : IWebSearchClient
{
    public async Task<SearchReply> SearchLearningAsync(string skill, string level, string language, CancellationToken ct)
    {
        var key = config["Search:ApiKey"]; if (string.IsNullOrWhiteSpace(key)) throw new IntegrationError("search_not_configured");
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.tavily.com/search");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        request.Content = JsonContent.Create(new { query = $"{skill} {level} tutorial " + (language == "vi" ? "tiếng Việt" : "English"),
            search_depth = "basic", auto_parameters = false, max_results = 3, topic = "general", include_answer = false, include_raw_content = false, include_images = false, include_usage = true });
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode) throw new IntegrationError(response.StatusCode == HttpStatusCode.TooManyRequests ? "search_provider_quota" : "search_provider_unavailable");
        using var data = JsonDocument.Parse(await IntegrationHttp.ReadLimited(response, ct));
        if (!data.RootElement.TryGetProperty("usage", out var usage) || !usage.TryGetProperty("credits", out var credit) || !credit.TryGetInt32(out var credits) || credits < 0 || credits > 100) throw new IntegrationError("search_usage_unknown");
        if (!data.RootElement.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array) throw new IntegrationError("search_output_invalid");
        var links = new List<LearningLink>();
        foreach (var item in results.EnumerateArray().Take(3))
        {
            if (item.ValueKind != JsonValueKind.Object) continue;
            if (!item.TryGetProperty("url", out var url) || url.ValueKind != JsonValueKind.String || !IntegrationHttp.SafePublicLink(url.GetString()!, out var uri) ||
                !item.TryGetProperty("title", out var title) || title.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(title.GetString())) continue;
            links.Add(new(title.GetString()![..Math.Min(250, title.GetString()!.Length)], uri!.AbsoluteUri, uri.IdnHost, language));
        }
        return new(links.DistinctBy(x => x.Url).ToArray(), credits);
    }
}
