using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

namespace CareerGap.Api.Matching;

public sealed record JdUrlPreviewResult(string FinalUrl, string Title, string Text, bool Truncated);

public sealed class JdUrlImportException(string code, int status) : Exception(code)
{
    public string Code { get; } = code;
    public int Status { get; } = status;
}

public sealed partial class JdUrlImporter
{
    private const int MaxRedirects = 3;
    private const int MaxResponseBytes = 512 * 1024;
    private const int MaxExtractedChars = 12000;
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(8);

    private readonly HttpClient http;
    private readonly Func<string, CancellationToken, Task<IPAddress[]>> resolveHost;

    public JdUrlImporter(HttpClient http, Func<string, CancellationToken, Task<IPAddress[]>>? resolveHost = null)
    {
        this.http = http;
        this.resolveHost = resolveHost ?? Dns.GetHostAddressesAsync;
    }

    [GeneratedRegex(@"<(script|style|noscript|svg|nav|footer|header)\b[^>]*>[\s\S]*?</\1>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex IgnoredBlocks();

    [GeneratedRegex(@"<title\b[^>]*>([\s\S]*?)</title>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TitleTag();

    [GeneratedRegex(@"<h1\b[^>]*>([\s\S]*?)</h1>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex H1Tag();

    [GeneratedRegex(@"<(?:br|p|div|li|h[1-6]|tr|section|article)\b[^>]*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BlockBreakTags();

    [GeneratedRegex(@"<[^>]+>", RegexOptions.CultureInvariant)]
    private static partial Regex AnyHtmlTag();

    public async Task<JdUrlPreviewResult> FetchAsync(string rawUrl, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(rawUrl) || rawUrl.Length > 2048 ||
            !Uri.TryCreate(rawUrl.Trim(), UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https") || !string.IsNullOrEmpty(uri.UserInfo) ||
            uri.IsDefaultPort == false && uri.Port is not (80 or 443))
            throw new JdUrlImportException("invalid_jd_url", 400);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(RequestTimeout);

        var current = uri;
        for (var hop = 0; hop <= MaxRedirects; hop++)
        {
            await EnsurePublicHostAsync(current, cts.Token);
            using var request = new HttpRequestMessage(HttpMethod.Get, current);
            request.Headers.UserAgent.ParseAdd("CareerGapAI-JDImporter/1.0");
            request.Headers.Accept.ParseAdd("text/html, text/plain;q=0.9");

            HttpResponseMessage response;
            try
            {
                response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                throw new JdUrlImportException("jd_url_timeout", 504);
            }
            catch (HttpRequestException)
            {
                throw new JdUrlImportException("jd_url_unreachable", 502);
            }

            using (response)
            {
                if ((int)response.StatusCode is >= 300 and <= 399 && response.Headers.Location is { } location)
                {
                    if (hop == MaxRedirects) throw new JdUrlImportException("jd_url_too_many_redirects", 422);
                    var next = location.IsAbsoluteUri ? location : new Uri(current, location);
                    if (next.Scheme is not ("http" or "https") || !string.IsNullOrEmpty(next.UserInfo))
                        throw new JdUrlImportException("invalid_jd_url", 400);
                    current = next;
                    continue;
                }

                if (!response.IsSuccessStatusCode)
                    throw new JdUrlImportException("jd_url_fetch_failed", 422);

                var mediaType = response.Content.Headers.ContentType?.MediaType?.ToLowerInvariant();
                if (mediaType is not (null or "text/html" or "application/xhtml+xml" or "text/plain"))
                    throw new JdUrlImportException("jd_url_unsupported_type", 422);

                var rawBody = await ReadBoundedTextAsync(response.Content, cts.Token);
                var isPlain = mediaType == "text/plain";
                var (title, text) = isPlain
                    ? ("JD từ URL", NormalizeWhitespace(rawBody))
                    : ExtractFromHtml(rawBody);

                if (string.IsNullOrWhiteSpace(text))
                    throw new JdUrlImportException("jd_url_empty_content", 422);

                var truncated = text.Length > MaxExtractedChars;
                if (truncated) text = text[..MaxExtractedChars];
                return new(current.ToString(), string.IsNullOrWhiteSpace(title) ? "JD từ URL" : title[..Math.Min(200, title.Length)], text, truncated);
            }
        }
        throw new JdUrlImportException("jd_url_too_many_redirects", 422);
    }

    private async Task EnsurePublicHostAsync(Uri uri, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(uri.DnsSafeHost) ||
            uri.DnsSafeHost.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
            uri.DnsSafeHost.EndsWith(".local", StringComparison.OrdinalIgnoreCase) ||
            uri.DnsSafeHost.EndsWith(".internal", StringComparison.OrdinalIgnoreCase))
            throw new JdUrlImportException("jd_url_blocked_host", 400);

        IPAddress[] addresses;
        if (IPAddress.TryParse(uri.DnsSafeHost, out var literal))
            addresses = [literal];
        else
        {
            try { addresses = await resolveHost(uri.DnsSafeHost, ct); }
            catch (SocketException) { throw new JdUrlImportException("jd_url_unreachable", 502); }
        }
        if (addresses.Length == 0 || addresses.Any(IsRestrictedAddress))
            throw new JdUrlImportException("jd_url_blocked_host", 400);
    }

    public static bool IsRestrictedAddress(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        if (IPAddress.IsLoopback(address)) return true;

        var bytes = address.GetAddressBytes();
        if (address.AddressFamily == AddressFamily.InterNetwork && bytes.Length == 4)
        {
            return bytes[0] switch
            {
                0 => true,                                            // 0.0.0.0/8
                10 => true,                                           // 10.0.0.0/8
                100 when bytes[1] >= 64 && bytes[1] <= 127 => true,   // 100.64.0.0/10 (CGNAT)
                127 => true,                                          // 127.0.0.0/8
                169 when bytes[1] == 254 => true,                     // 169.254.0.0/16 (link-local / metadata)
                172 when bytes[1] >= 16 && bytes[1] <= 31 => true,    // 172.16.0.0/12
                192 when bytes[1] == 168 => true,                     // 192.168.0.0/16
                192 when bytes[1] == 0 && bytes[2] == 0 => true,      // 192.0.0.0/24
                198 when bytes[1] is 18 or 19 => true,                // 198.18.0.0/15
                >= 224 => true,                                       // multicast & reserved
                _ => false
            };
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6 && bytes.Length == 16)
        {
            if (address.Equals(IPAddress.IPv6None) || address.Equals(IPAddress.IPv6Any)) return true;
            if (address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6Multicast) return true;
            if ((bytes[0] & 0xFE) == 0xFC) return true; // fc00::/7 Unique Local Address
        }
        return false;
    }

    private static async Task<string> ReadBoundedTextAsync(HttpContent content, CancellationToken ct)
    {
        if (content.Headers.ContentLength is > MaxResponseBytes)
            throw new JdUrlImportException("jd_url_too_large", 413);
        await using var stream = await content.ReadAsStreamAsync(ct);
        using var buffer = new MemoryStream();
        var chunk = new byte[8192];
        int read;
        while ((read = await stream.ReadAsync(chunk, ct)) > 0)
        {
            if (buffer.Length + read > MaxResponseBytes)
                throw new JdUrlImportException("jd_url_too_large", 413);
            buffer.Write(chunk, 0, read);
        }
        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    private static (string Title, string Text) ExtractFromHtml(string html)
    {
        var titleMatch = H1Tag().Match(html);
        if (!titleMatch.Success) titleMatch = TitleTag().Match(html);
        var title = titleMatch.Success ? NormalizeWhitespace(WebUtility.HtmlDecode(AnyHtmlTag().Replace(titleMatch.Groups[1].Value, " "))) : "";
        var cleaned = IgnoredBlocks().Replace(html, " ");
        cleaned = BlockBreakTags().Replace(cleaned, "\n");
        cleaned = AnyHtmlTag().Replace(cleaned, " ");
        cleaned = WebUtility.HtmlDecode(cleaned);
        return (title, NormalizeWhitespace(cleaned));
    }

    private static string NormalizeWhitespace(string value)
    {
        var lines = value.ReplaceLineEndings("\n").Split('\n')
            .Select(line => Regex.Replace(line, @"[ \t]+", " ", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(50)).Trim())
            .Where(line => line.Length > 0);
        return string.Join('\n', lines);
    }
}
