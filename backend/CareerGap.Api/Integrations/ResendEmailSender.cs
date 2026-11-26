using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Mail;
using System.Text.Json;
using CareerGap.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
namespace CareerGap.Api.Integrations;
public sealed class EmailOptions
{
    public string Provider { get; set; } = "Fake";
    public string Sender { get; set; } = "";
    public bool SenderVerified { get; set; }
    public string TestRecipient { get; set; } = "";
    public int DeliveryLimit { get; set; }
    public string PeriodStart { get; set; } = "";
    public string PeriodEnd { get; set; } = "";
    public string LimitsVerifiedAt { get; set; } = "";
    public bool Ready(DateTimeOffset now) => Provider == "Resend" && SenderVerified && DeliveryLimit > 0 && MailAddress.TryCreate(Sender, out _) && MailAddress.TryCreate(TestRecipient, out _) &&
        DateTimeOffset.TryParse(PeriodStart, out var start) && DateTimeOffset.TryParse(PeriodEnd, out var end) && start <= now && end > now && end - start <= TimeSpan.FromDays(32) &&
        DateTimeOffset.TryParse(LimitsVerifiedAt, out var verified) && verified <= now && verified > now.AddDays(-31);
}
// Local verification mode only: one owner-controlled recipient, no automatic retries.
public sealed class ResendEmailSender(HttpClient http, AppDb db, IOptions<EmailOptions> options, IConfiguration config, TimeProvider clock) : IEmailSender
{
    public Task SendPasswordRecoveryAsync(string recipient, Uri recoveryLink, CancellationToken ct) => SendAsync(recipient,recoveryLink,"CareerGap AI — Khôi phục mật khẩu","Bạn đã yêu cầu đặt lại mật khẩu.",ct);
    public Task SendBusinessVerificationAsync(string recipient, Uri link, CancellationToken ct) => SendAsync(recipient,link,"CareerGap AI — Xác minh email doanh nghiệp","Xác minh quyền truy cập email doanh nghiệp. Liên kết này không xác nhận tư cách pháp nhân.",ct);
    private async Task SendAsync(string recipient, Uri recoveryLink,string subject,string introduction,CancellationToken ct)
    {
        var opt = options.Value; var now = clock.GetUtcNow(); var key = config["Email:ApiKey"];
        if (!opt.Ready(now) || string.IsNullOrWhiteSpace(key) || !string.Equals(recipient, opt.TestRecipient, StringComparison.OrdinalIgnoreCase)) throw new IntegrationError("email_not_configured");
        var start = DateTimeOffset.Parse(opt.PeriodStart).ToUniversalTime(); var end = DateTimeOffset.Parse(opt.PeriodEnd).ToUniversalTime();
        EmailDelivery delivery;
        await using (var tx = await db.Database.BeginTransactionAsync(ct))
        {
            await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(341019)", ct);
            if (await db.EmailDeliveries.AnyAsync(x => x.Provider == opt.Provider && x.PeriodStart < end && x.PeriodEnd > start && (x.PeriodStart != start || x.PeriodEnd != end), ct)) throw new IntegrationError("email_period_conflict");
            if (await db.EmailDeliveries.CountAsync(x => x.Provider == opt.Provider && x.PeriodStart == start, ct) >= opt.DeliveryLimit) throw new IntegrationError("email_quota_exhausted");
            delivery = new() { Provider = opt.Provider, PeriodStart = start, PeriodEnd = end, CreatedAt = now }; db.EmailDeliveries.Add(delivery); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        }
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.resend.com/emails");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key); request.Headers.Add("Idempotency-Key", "recovery-"+delivery.Id.ToString("N"));
            request.Content = JsonContent.Create(new { from = opt.Sender, to = new[] { recipient }, subject, text = introduction+" Mở liên kết này trên máy đang chạy CareerGap AI localhost:\n"+recoveryLink.AbsoluteUri+"\nNếu không yêu cầu, hãy bỏ qua thư này." });
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(15));
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if (!response.IsSuccessStatusCode) throw new IntegrationError("email_provider_unavailable");
            using var body = JsonDocument.Parse(await IntegrationHttp.ReadLimited(response, timeout.Token));
            if (!body.RootElement.TryGetProperty("id", out var id) || id.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(id.GetString())) throw new IntegrationError("email_output_invalid");
            delivery.Status = "accepted"; await db.SaveChangesAsync(CancellationToken.None);
        }
        catch (Exception error) when (error is HttpRequestException or OperationCanceledException or IntegrationError or JsonException)
        { delivery.Status = "outcome_unknown"; await db.SaveChangesAsync(CancellationToken.None); throw new IntegrationError("email_delivery_unknown"); }
    }
}
