using System.Net;
using CareerGap.Api.Data;
using CareerGap.Api.Integrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;
namespace CareerGap.Api.Tests;
public sealed class EmailAdapterTests(FoundationFixture fixture) : IClassFixture<FoundationFixture>
{
    private sealed class Handler : HttpMessageHandler
    {
        public int Calls; public bool Fail; public HashSet<string> Keys = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++; Assert.Equal("https://api.resend.com/emails", request.RequestUri!.AbsoluteUri); Assert.True(Keys.Add(request.Headers.GetValues("Idempotency-Key").Single()));
            if (Fail) throw new HttpRequestException("offline");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"id\":\"synthetic-receipt\"}") });
        }
    }
    [Fact] public async Task EmailReadinessQuotaAndUnknownOutcomeAreDurableWithoutTokenStorage()
    {
        using var scope = fixture.App.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDb>(); var handler = new Handler();
        var options = new EmailOptions { Provider="Resend", Sender="sender@example.invalid", TestRecipient="controlled@example.invalid", SenderVerified=true, DeliveryLimit=2,
            PeriodStart=DateTimeOffset.UtcNow.AddMinutes(-5).ToString("O"), PeriodEnd=DateTimeOffset.UtcNow.AddDays(1).ToString("O"), LimitsVerifiedAt=DateTimeOffset.UtcNow.AddMinutes(-1).ToString("O") };
        var service = new ResendEmailSender(new HttpClient(handler), db, Options.Create(options), new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["Email:ApiKey"]="offline-test-only" }).Build(), TimeProvider.System);
        var link = new Uri("http://127.0.0.1:5173/?resetToken=synthetic-secret");
        await Assert.ThrowsAsync<IntegrationError>(()=>service.SendPasswordRecoveryAsync("uncontrolled@example.invalid",link,default)); Assert.Equal(0,handler.Calls);
        await service.SendPasswordRecoveryAsync(options.TestRecipient,link,default); Assert.Equal("accepted",(await db.EmailDeliveries.SingleAsync()).Status);
        handler.Fail=true; await Assert.ThrowsAsync<IntegrationError>(()=>service.SendPasswordRecoveryAsync(options.TestRecipient,link,default));
        Assert.Equal(2,handler.Calls); Assert.Equal(1,await db.EmailDeliveries.CountAsync(x=>x.Status=="outcome_unknown"));
        Assert.Equal("email_quota_exhausted",(await Assert.ThrowsAsync<IntegrationError>(()=>service.SendPasswordRecoveryAsync(options.TestRecipient,link,default))).Code); Assert.Equal(2,handler.Calls);
        var columns = db.Model.FindEntityType(typeof(EmailDelivery))!.GetProperties().Select(x=>x.Name).ToArray(); Assert.DoesNotContain("Recipient",columns); Assert.DoesNotContain("Link",columns);
    }
}
