namespace CareerGap.Api.Integrations;
using System.Collections.Concurrent;

public sealed record LearningLink(string Title, string Url, string Source, string Language);
public sealed record SearchReply(IReadOnlyList<LearningLink> Links, int? CreditsUsed);
public interface IWebSearchClient
{
    Task<SearchReply> SearchLearningAsync(string skill, string level, string language, CancellationToken ct);
}
public interface IEmailSender
{
    Task SendPasswordRecoveryAsync(string recipient, Uri recoveryLink, CancellationToken ct);
    Task SendBusinessVerificationAsync(string recipient, Uri link, CancellationToken ct) => throw new IntegrationError("email_not_configured");
}
public sealed record LocalPasswordRecovery(string Recipient, string Link, DateTimeOffset CreatedAt);
public sealed class LocalMailOutbox
{
    private readonly ConcurrentQueue<LocalPasswordRecovery> messages = new();
    public void Add(LocalPasswordRecovery message) => messages.Enqueue(message);
    public IReadOnlyList<LocalPasswordRecovery> Drain()
    {
        var result = new List<LocalPasswordRecovery>();
        while (messages.TryDequeue(out var message)) result.Add(message);
        return result;
    }
}
// No fabricated URLs or simulated delivery receipts. Real adapters remain blocked by provider readiness.
public sealed class FakeWebSearchClient : IWebSearchClient
{
    public Task<SearchReply> SearchLearningAsync(string skill, string level, string language, CancellationToken ct)
    { ct.ThrowIfCancellationRequested(); return Task.FromResult(new SearchReply([], 0)); }
}
public sealed class FakeEmailSender(LocalMailOutbox outbox) : IEmailSender
{
    public Task SendBusinessVerificationAsync(string recipient, Uri link, CancellationToken ct)
    { ct.ThrowIfCancellationRequested(); outbox.Add(new LocalPasswordRecovery(recipient,link.ToString(),DateTimeOffset.UtcNow)); return Task.CompletedTask; }
    public Task SendPasswordRecoveryAsync(string recipient, Uri recoveryLink, CancellationToken ct)
    { ct.ThrowIfCancellationRequested(); outbox.Add(new LocalPasswordRecovery(recipient, recoveryLink.ToString(), DateTimeOffset.UtcNow)); return Task.CompletedTask; }
}
