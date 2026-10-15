namespace CareerGap.Api.Integrations;

public sealed record LearningLink(string Title, string Url, string Source, string Language);
public interface IWebSearchClient
{
    Task<IReadOnlyList<LearningLink>> SearchLearningAsync(string skill, string level, string language, CancellationToken ct);
}
public interface IEmailSender
{
    Task SendPasswordRecoveryAsync(string recipient, Uri recoveryLink, CancellationToken ct);
}
// No fabricated URLs or simulated delivery receipts. Real adapters remain blocked by provider readiness.
public sealed class FakeWebSearchClient : IWebSearchClient
{
    public Task<IReadOnlyList<LearningLink>> SearchLearningAsync(string skill, string level, string language, CancellationToken ct)
    { ct.ThrowIfCancellationRequested(); return Task.FromResult<IReadOnlyList<LearningLink>>([]); }
}
public sealed class FakeEmailSender : IEmailSender
{
    public Task SendPasswordRecoveryAsync(string recipient, Uri recoveryLink, CancellationToken ct)
    { ct.ThrowIfCancellationRequested(); return Task.CompletedTask; }
}
