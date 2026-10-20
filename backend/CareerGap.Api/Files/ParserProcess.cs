using System.Diagnostics;
using System.Text.Json;

namespace CareerGap.Api.Files;

public sealed record ParserRequest(string Name, byte[] Bytes);
public sealed record ParserReply(ParsedDocument? Document, string? Error);

public static class ParserProcess
{
    public static ParsedDocument Parse(byte[] bytes, string name, CancellationToken ct, TimeSpan? limit = null)
        => ParseAsync(bytes, name, ct, limit ?? TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();

    private static async Task<ParsedDocument> ParseAsync(byte[] bytes, string name, CancellationToken ct, TimeSpan limit)
    {
        if (bytes.Length is 0 or > DocumentParser.MaxBytes) throw new DocumentError("file_size");
        ct.ThrowIfCancellationRequested();
        var assembly = typeof(DocumentParser).Assembly.Location;
        var start = new ProcessStartInfo("dotnet") { RedirectStandardInput = true, RedirectStandardOutput = true,
            RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        start.ArgumentList.Add(assembly); start.ArgumentList.Add("--parse-document-worker");
        // No provider/database/session configuration is inherited by the parser process.
        var allowed = new[] { "PATH", "SystemRoot", "WINDIR", "TEMP", "TMP", "TMPDIR", "DOTNET_ROOT", "HOME", "USERPROFILE" };
        var environment = allowed.ToDictionary(x => x, Environment.GetEnvironmentVariable);
        start.Environment.Clear();
        foreach (var item in environment) if (item.Value is not null) start.Environment[item.Key] = item.Value;
        start.Environment["DOTNET_NOLOGO"] = "1";
        start.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        using var process = new Process { StartInfo = start };
        var started = false;
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(limit);
        try
        {
            if (!process.Start()) throw new DocumentError("parser_unavailable");
            started = true;
            var read = ReadBounded(process.StandardOutput, 512000, deadline.Token);
            var errors = ReadBounded(process.StandardError, 8192, deadline.Token);
            await process.StandardInput.WriteAsync(JsonSerializer.Serialize(new ParserRequest(Path.GetExtension(name), bytes)).AsMemory(), deadline.Token);
            process.StandardInput.Close();
            await process.WaitForExitAsync(deadline.Token);
            var raw = await read; await errors;
            var result = JsonSerializer.Deserialize<ParserReply>(raw);
            if (result?.Error is not null) throw new DocumentError(result.Error);
            if (process.ExitCode != 0 || result?.Document is null) throw new DocumentError("file_unreadable");
            return result.Document;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw new DocumentError("parse_timeout"); }
        catch (OperationCanceledException) { throw; }
        catch (DocumentError) { throw; }
        catch (Exception error) when (error is not OutOfMemoryException) { throw new DocumentError("parser_unavailable"); }
        finally
        {
            if (started && !process.HasExited)
            { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(CancellationToken.None); }
        }
    }

    private static async Task<string> ReadBounded(TextReader reader, int maximum, CancellationToken ct)
    {
        var buffer = new char[4096]; var text = new System.Text.StringBuilder(); int count;
        while ((count = await reader.ReadAsync(buffer.AsMemory(), ct)) > 0)
        { if (text.Length + count > maximum) throw new DocumentError("text_limit"); text.Append(buffer, 0, count); }
        return text.ToString();
    }

    public static async Task RunWorker()
    {
        ParserReply reply;
        try
        {
            var raw = await ReadBounded(Console.In, 6 * 1024 * 1024, CancellationToken.None);
            var request = JsonSerializer.Deserialize<ParserRequest>(raw) ?? throw new DocumentError("file_format");
            reply = new(new DocumentParser().ParseCore(request.Bytes, request.Name, CancellationToken.None), null);
        }
        catch (DocumentError error) { reply = new(null, error.Code); }
        catch (Exception error) when (error is not OutOfMemoryException) { reply = new(null, "file_unreadable"); }
        await Console.Out.WriteAsync(JsonSerializer.Serialize(reply));
    }
}
