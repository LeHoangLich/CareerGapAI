using System.Buffers.Binary;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace CareerGap.Api.Files;

public sealed record ParserRequest(string Name, byte[] Bytes);
public sealed record ParserReply(ParsedDocument? Document, string? Error);

public static class ParserProcess
{
    private const int MaxExtensionBytes = 32;

    public static ParsedDocument Parse(byte[] bytes, string name, CancellationToken ct, TimeSpan? limit = null)
        => ParseAsync(bytes, name, ct, limit ?? TimeSpan.FromSeconds(25)).GetAwaiter().GetResult();

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
        start.Environment["DOTNET_gcServer"] = "0";
        start.Environment["DOTNET_GCHeapHardLimit"] = "0x06000000";
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
            await WriteBinaryFrameAsync(process.StandardInput.BaseStream, Path.GetExtension(name), bytes, deadline.Token);
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

    private static async Task WriteBinaryFrameAsync(Stream stream, string extension, byte[] bytes, CancellationToken ct)
    {
        var extBytes = Encoding.UTF8.GetBytes(extension);
        if (extBytes.Length > MaxExtensionBytes) extBytes = extBytes[..MaxExtensionBytes];
        var header = new byte[8];
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(0, 4), extBytes.Length);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(4, 4), bytes.Length);
        await stream.WriteAsync(header, ct);
        if (extBytes.Length > 0) await stream.WriteAsync(extBytes, ct);
        await stream.WriteAsync(bytes, ct);
        await stream.FlushAsync(ct);
    }

    private static async Task<(string Extension, byte[] Bytes)> ReadBinaryFrameAsync(Stream stream, CancellationToken ct)
    {
        var header = new byte[8];
        await ReadExactAsync(stream, header, ct);
        var extLength = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(0, 4));
        var payloadLength = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(4, 4));
        if (extLength is < 0 or > MaxExtensionBytes) throw new DocumentError("file_format");
        if (payloadLength is <= 0 or > DocumentParser.MaxBytes) throw new DocumentError("file_size");
        var extBytes = new byte[extLength];
        if (extLength > 0) await ReadExactAsync(stream, extBytes, ct);
        var payload = new byte[payloadLength];
        await ReadExactAsync(stream, payload, ct);
        return (Encoding.UTF8.GetString(extBytes), payload);
    }

    private static async Task ReadExactAsync(Stream stream, byte[] buffer, CancellationToken ct)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(offset), ct);
            if (read == 0) throw new DocumentError("file_unreadable");
            offset += read;
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
            using var input = Console.OpenStandardInput();
            var (extension, bytes) = await ReadBinaryFrameAsync(input, CancellationToken.None);
            reply = new(new DocumentParser().ParseCore(bytes, extension, CancellationToken.None), null);
        }
        catch (DocumentError error) { reply = new(null, error.Code); }
        catch (Exception error) when (error is not OutOfMemoryException) { reply = new(null, "file_unreadable"); }
        await Console.Out.WriteAsync(JsonSerializer.Serialize(reply));
    }
}
