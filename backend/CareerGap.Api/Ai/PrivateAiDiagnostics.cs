using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Hosting;

namespace CareerGap.Api.Ai;

public static class PrivateAiDiagnostics
{
    // Explicit opt-in only on the local development host. Never expose these files through an API.
    public static async Task SaveRejected(IServiceProvider services, Guid job, Guid? attempt, LlmReply? reply,
        string error, CancellationToken ct)
    {
        var environment = services.GetRequiredService<IHostEnvironment>();
        if (reply is null || attempt is null || !environment.IsDevelopment() ||
            !services.GetRequiredService<IConfiguration>().GetValue<bool>("Ai:PrivateDiagnostics")) return;
        var directory = Path.GetFullPath(Path.Combine(environment.ContentRootPath, "../../.local/ai-diagnostics"));
        var webRoot = Path.GetFullPath(Path.Combine(environment.ContentRootPath, "wwwroot")) + Path.DirectorySeparatorChar;
        if (directory.StartsWith(webRoot, StringComparison.OrdinalIgnoreCase)) return;
        try
        {
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, $"{job:N}-{attempt:N}.json");
            await using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { job, attempt, error, reply }, OutputValidator.Json));
            await file.WriteAsync(bytes, ct);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            services.GetRequiredService<ILoggerFactory>().CreateLogger("PrivateAiDiagnostics")
                .LogWarning("Private AI diagnostics could not be written ({ExceptionType})", ex.GetType().Name);
        }
    }
}
