using CareerGap.Api.Ai;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace CareerGap.Api.Tests;

public sealed class PrivateAiDiagnosticsTests
{
    private sealed class EnvironmentStub(string environment, string contentRoot) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environment;
        public string ApplicationName { get; set; } = "SyntheticDiagnostics";
        public string ContentRootPath { get; set; } = contentRoot;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
    [Theory]
    [InlineData("Development", false, false)]
    [InlineData("Production", true, false)]
    [InlineData("Testing", true, false)]
    [InlineData("Development", true, true)]
    public async Task RejectedRepliesRequireExplicitLocalDevelopmentOptIn(string environment, bool enabled, bool expected)
    {
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "careergap-diag-" + Guid.NewGuid().ToString("N")));
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Ai:PrivateDiagnostics"] = enabled.ToString() }).Build();
        using var services = new ServiceCollection().AddSingleton<IConfiguration>(configuration)
            .AddSingleton<IHostEnvironment>(new EnvironmentStub(environment, Path.Combine(root, "backend", "api"))).AddLogging().BuildServiceProvider();
        var job = Guid.NewGuid(); var attempt = Guid.NewGuid();
        var path = Path.Combine(root, ".local", "ai-diagnostics", $"{job:N}-{attempt:N}.json");
        try
        {
            await PrivateAiDiagnostics.SaveRejected(services, job, attempt, new("{\"synthetic\":true}", 7, 8), "ai_output_invalid", default);
            Assert.Equal(expected, File.Exists(path));
            if (expected)
            {
                Assert.Contains("synthetic", await File.ReadAllTextAsync(path));
                Assert.DoesNotContain("wwwroot", path);
                // The same attempt cannot overwrite its retained original reply.
                var bytes = await File.ReadAllBytesAsync(path);
                await PrivateAiDiagnostics.SaveRejected(services, job, attempt, new("changed", 7, 8), "ai_output_invalid", default);
                Assert.Equal(bytes, await File.ReadAllBytesAsync(path));
            }
        }
        finally
        {
            var temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            Assert.StartsWith(temp, root, StringComparison.OrdinalIgnoreCase);
            Assert.StartsWith("careergap-diag-", Path.GetFileName(root));
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }
}
