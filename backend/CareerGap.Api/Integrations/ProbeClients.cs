namespace CareerGap.Api.Integrations;
using System.Collections.Concurrent;

public sealed record LearningLink(string Title, string Url, string Source, string Language);
public interface IWebSearchClient
{
    Task<IReadOnlyList<LearningLink>> SearchLearningAsync(string skill, string level, string language, CancellationToken ct);
}
public interface IEmailSender
{
    Task SendPasswordRecoveryAsync(string recipient, Uri recoveryLink, CancellationToken ct);
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
    public Task<IReadOnlyList<LearningLink>> SearchLearningAsync(string skill, string level, string language, CancellationToken ct)
    { ct.ThrowIfCancellationRequested(); return Task.FromResult<IReadOnlyList<LearningLink>>([]); }
}
public sealed class FakeEmailSender(LocalMailOutbox outbox) : IEmailSender
{
    public Task SendPasswordRecoveryAsync(string recipient, Uri recoveryLink, CancellationToken ct)
    { ct.ThrowIfCancellationRequested(); outbox.Add(new LocalPasswordRecovery(recipient, recoveryLink.ToString(), DateTimeOffset.UtcNow)); return Task.CompletedTask; }
}
