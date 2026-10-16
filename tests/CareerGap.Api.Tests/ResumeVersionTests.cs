using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CareerGap.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace CareerGap.Api.Tests;

public sealed class ResumeVersionTests(FoundationFixture fixture) : IClassFixture<FoundationFixture>
{
    [Fact]
    public async Task CandidateOwnsNamedResumeWithImmutablePrivateVersionsAndConcurrentAddsGetDistinctNumbers()
    {
        var (owner, ownerId) = await fixture.User();
        var (otherCandidate, _) = await fixture.User();
        var (recruiter, _) = await fixture.User("Recruiter");
        var first = await FoundationFixture.Upload(owner, FoundationFixture.Docx("Phiên bản đầu tiên"), "backend-v1.docx");
        var resumeId = first.GetProperty("id").GetGuid();
        var firstVersionId = first.GetProperty("versionId").GetGuid();
        var firstFileId = first.GetProperty("fileId").GetGuid();

        using var forbiddenAdd = await Attach(otherCandidate, resumeId, "other-owner.docx", "Không được gắn vào CV của người khác");
        Assert.Equal(HttpStatusCode.NotFound, forbiddenAdd.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await recruiter.GetAsync("/api/resumes")).StatusCode);

        var uploaded = await Task.WhenAll(
            AttachVersion(owner, resumeId, "backend-v2-a.docx", "Nội dung giả phiên bản hai A"),
            AttachVersion(owner, resumeId, "backend-v2-b.docx", "Nội dung giả phiên bản hai B"));
        Assert.Equal(new[] { 2, 3 }, uploaded.Select(x => x.GetProperty("versionNo").GetInt32()).Order().ToArray());
        Assert.All(uploaded, result => Assert.Equal(resumeId, result.GetProperty("id").GetGuid()));

        var resumes = await owner.GetFromJsonAsync<JsonElement[]>("/api/resumes?page=1");
        var listed = Assert.Single(resumes!);
        Assert.Equal(resumeId, listed.GetProperty("id").GetGuid());
        Assert.Equal(3, listed.GetProperty("versions").GetArrayLength());
        var rowVersion = listed.GetProperty("rowVersion").GetInt64();
        Assert.Equal(2, rowVersion);

        var renamed = await owner.PutAsJsonAsync($"/api/resumes/{resumeId}", new { rowVersion, title = "CV Backend chính" });
        Assert.Equal(HttpStatusCode.OK, renamed.StatusCode);
        var renamedContent = await renamed.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("CV Backend chính", renamedContent.GetProperty("title").GetString());
        Assert.Equal(3, renamedContent.GetProperty("rowVersion").GetInt64());
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PutAsJsonAsync($"/api/resumes/{resumeId}", new { rowVersion, title = "Tên cũ" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await otherCandidate.PutAsJsonAsync($"/api/resumes/{resumeId}", new { rowVersion = 0, title = "Không được sửa" })).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync($"/api/files/{firstFileId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await otherCandidate.GetAsync($"/api/files/{firstFileId}")).StatusCode);
        using (var scope = fixture.App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            var immutable = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE resume_versions SET document_hash = repeat('a',64) WHERE id = {firstVersionId}"));
            Assert.Equal("23514", immutable.SqlState);
            var ownerGuard = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE resumes SET candidate_id = {Guid.NewGuid()} WHERE id = {resumeId}"));
            Assert.Equal("23514", ownerGuard.SqlState);

            var pendingFile = new StoredFile { OwnerUserId = ownerId, ObjectKey = Guid.NewGuid().ToString("N"), Provider = "Local",
                OriginalName = "pending.docx", MimeType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                SizeBytes = 100, Status = "pending" };
            db.StoredFiles.Add(pendingFile);
            await db.SaveChangesAsync();
            db.ResumeVersions.Add(new ResumeVersion { ResumeId = resumeId, CandidateId = ownerId, FileId = pendingFile.Id,
                VersionNo = 100, DocumentHash = new string('A', 64) });
            var unverified = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            Assert.Equal("23514", Assert.IsType<PostgresException>(unverified.InnerException).SqlState);
        }
    }

    private static async Task<HttpResponseMessage> Attach(HttpClient client, Guid resumeId, string name, string text)
    {
        using var content = UploadContent(name, text);
        return await client.PostAsync($"/api/resumes/{resumeId}/versions", content);
    }

    private static async Task<JsonElement> AttachVersion(HttpClient client, Guid resumeId, string name, string text)
    {
        using var content = UploadContent(name, text);
        using var response = await client.PostAsync($"/api/resumes/{resumeId}/versions", content);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static MultipartFormDataContent UploadContent(string name, string text)
    {
        var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(FoundationFixture.Docx(text)), "file", name);
        return content;
    }
}
