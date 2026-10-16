using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CareerGap.Api.Data;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CareerGap.Api.Tests;

public sealed class PublicResumeTests(FoundationFixture fixture) : IClassFixture<FoundationFixture>
{
    [Fact]
    public async Task PublicResumePinsVersionAndServesOnlyVerifiedRedactionToOtherCandidates()
    {
        var (owner, ownerId) = await fixture.User();
        var (otherCandidate, _) = await fixture.User();
        var (recruiter, _) = await fixture.User("Recruiter");
        using var anonymous = fixture.App.CreateClient();
        var original = ContactRichDocx("Synthetic profile. Email jane.doe@example.invalid, alternate mail alex [at] localhost, phone +84 912 345 678. Link https://linkedin.com/in/jane-doe. Skills: React and PostgreSQL.");
        var upload = await FoundationFixture.Upload(owner, original, "synthetic-contact-cv.docx");
        var resumeVersionId = upload.GetProperty("versionId").GetGuid();
        var originalFileId = upload.GetProperty("fileId").GetGuid();

        var publicFilePath = $"/api/public/resumes/{ownerId}/{resumeVersionId}/file";
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(publicFilePath)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await recruiter.GetAsync("/api/resumes/public/me")).StatusCode);
        using (var emptySelection = await owner.GetAsync("/api/resumes/public/me"))
        {
            Assert.Equal(HttpStatusCode.OK, emptySelection.StatusCode);
            Assert.Equal("null", await emptySelection.Content.ReadAsStringAsync());
        }
        using var firstPublish = await owner.PostAsJsonAsync("/api/resumes/public", new { resumeVersionId, expectedRowVersion = (long?)null });
        Assert.Equal(HttpStatusCode.OK, firstPublish.StatusCode);
        var selection = await firstPublish.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("ready", selection.GetProperty("status").GetString());
        Assert.Equal(resumeVersionId, selection.GetProperty("resumeVersionId").GetGuid());

        var candidateList = await otherCandidate.GetFromJsonAsync<JsonElement[]>("/api/public/resumes?page=1");
        var candidateItem = Assert.Single(candidateList!, x => x.GetProperty("candidateId").GetGuid() == ownerId);
        Assert.Equal("Ứng viên", candidateItem.GetProperty("displayName").GetString());
        Assert.Equal("ready", candidateItem.GetProperty("redactionStatus").GetString());
        var recruiterList = await recruiter.GetFromJsonAsync<JsonElement[]>("/api/public/resumes?page=1");
        Assert.Single(recruiterList!, x => x.GetProperty("candidateId").GetGuid() == ownerId);

        using var candidateFile = await otherCandidate.GetAsync(publicFilePath);
        Assert.Equal(HttpStatusCode.OK, candidateFile.StatusCode);
        var derived = await candidateFile.Content.ReadAsByteArrayAsync();
        Assert.NotEqual(original, derived);
        using (var archive = new ZipArchive(new MemoryStream(derived), ZipArchiveMode.Read))
        {
            Assert.True(new HashSet<string>(archive.Entries.Select(x => x.FullName), StringComparer.Ordinal)
                .SetEquals(["[Content_Types].xml", "_rels/.rels", "word/document.xml"]));
            using var reader = new StreamReader(archive.GetEntry("word/document.xml")!.Open());
            var text = await reader.ReadToEndAsync();
            Assert.DoesNotContain("jane.doe@example.invalid", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("alex", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("+84 912 345 678", text, StringComparison.Ordinal);
            Assert.DoesNotContain("linkedin.com", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("hyperlink", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("PostgreSQL", text, StringComparison.Ordinal);
        }

        using var recruiterFile = await recruiter.GetAsync(publicFilePath);
        Assert.Equal(HttpStatusCode.OK, recruiterFile.StatusCode);
        Assert.Equal(original, await recruiterFile.Content.ReadAsByteArrayAsync());
        Assert.Equal(HttpStatusCode.NotFound, (await otherCandidate.GetAsync($"/api/files/{originalFileId}")).StatusCode);

        using var newVersionContent = new MultipartFormDataContent();
        newVersionContent.Add(new ByteArrayContent(FoundationFixture.Docx("New private version, still not public.")), "file", "private-v2.docx");
        using var newVersionResponse = await owner.PostAsync($"/api/resumes/{upload.GetProperty("id").GetGuid()}/versions", newVersionContent);
        Assert.Equal(HttpStatusCode.Created, newVersionResponse.StatusCode);
        var stillPinned = await owner.GetFromJsonAsync<JsonElement>("/api/resumes/public/me");
        Assert.Equal(resumeVersionId, stillPinned.GetProperty("resumeVersionId").GetGuid());

        var newVersion = await newVersionResponse.Content.ReadFromJsonAsync<JsonElement>();
        var newVersionId = newVersion.GetProperty("versionId").GetGuid();
        using var replace = await owner.PostAsJsonAsync("/api/resumes/public", new
        {
            resumeVersionId = newVersionId,
            expectedRowVersion = stillPinned.GetProperty("rowVersion").GetInt64()
        });
        Assert.Equal(HttpStatusCode.OK, replace.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await otherCandidate.GetAsync(publicFilePath)).StatusCode);
        using var replacedPublicFile = await otherCandidate.GetAsync($"/api/public/resumes/{ownerId}/{newVersionId}/file");
        Assert.Equal(HttpStatusCode.OK, replacedPublicFile.StatusCode);

        var replaced = await replace.Content.ReadFromJsonAsync<JsonElement>();
        var rowVersion = replaced.GetProperty("rowVersion").GetInt64();
        Assert.Equal(HttpStatusCode.NoContent, (await owner.DeleteAsync($"/api/resumes/public/me?rowVersion={rowVersion}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await otherCandidate.GetAsync($"/api/public/resumes/{ownerId}/{newVersionId}/file")).StatusCode);
        var afterHide = await recruiter.GetFromJsonAsync<JsonElement[]>("/api/public/resumes?page=1");
        Assert.DoesNotContain(afterHide!, x => x.GetProperty("candidateId").GetGuid() == ownerId);
        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync($"/api/files/{originalFileId}")).StatusCode);
    }

    [Fact]
    public async Task ConcurrentFirstPublicationsWithSameExpectedStateCommitOnlyOneChoice()
    {
        var (owner, ownerId) = await fixture.User();
        var (otherCandidate, _) = await fixture.User();
        var upload = await FoundationFixture.Upload(owner, FoundationFixture.Docx("Synthetic resume content with enough text to create a safe derivative."));
        var versionId = upload.GetProperty("versionId").GetGuid();

        var results = await Task.WhenAll(
            owner.PostAsJsonAsync("/api/resumes/public", new { resumeVersionId = versionId, expectedRowVersion = (long?)null }),
            owner.PostAsJsonAsync("/api/resumes/public", new { resumeVersionId = versionId, expectedRowVersion = (long?)null }));
        try
        {
            Assert.Single(results, x => x.StatusCode == HttpStatusCode.OK);
            Assert.Single(results, x => x.StatusCode == HttpStatusCode.Conflict);
        }
        finally { foreach (var response in results) response.Dispose(); }

        var selection = await owner.GetFromJsonAsync<JsonElement>("/api/resumes/public/me");
        Assert.Equal("ready", selection.GetProperty("status").GetString());
        Assert.Equal(0, selection.GetProperty("rowVersion").GetInt64());
        var visibleToOther = await otherCandidate.GetFromJsonAsync<JsonElement[]>("/api/public/resumes?page=1");
        Assert.Single(visibleToOther!, x => x.GetProperty("candidateId").GetGuid() == ownerId);
        using var scope = fixture.App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        Assert.Equal(1, await db.CandidatePublicResumes.CountAsync(x => x.CandidateId == ownerId));
    }

    [Fact]
    public async Task PdfLinksAndMetadataAreNotCarriedIntoCandidateDerivative()
    {
        var (owner, ownerId) = await fixture.User();
        var (otherCandidate, _) = await fixture.User();
        var (recruiter, _) = await fixture.User("Recruiter");
        var original = MinimalPdf("Synthetic CV email jane.doe@example.invalid phone +84 912 345 678 skills React PostgreSQL",
            "Contact jane.doe@example.invalid +84 912 345 678 https://linkedin.com/in/jane");
        var uploaded = await FoundationFixture.Upload(owner, original, "synthetic-public.pdf");
        var versionId = uploaded.GetProperty("versionId").GetGuid();
        using var publish = await owner.PostAsJsonAsync("/api/resumes/public", new { resumeVersionId = versionId, expectedRowVersion = (long?)null });
        Assert.Equal(HttpStatusCode.OK, publish.StatusCode);
        var selection = await publish.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("ready", selection.GetProperty("status").GetString());

        using var candidateFile = await otherCandidate.GetAsync($"/api/public/resumes/{ownerId}/{versionId}/file");
        Assert.Equal(HttpStatusCode.OK, candidateFile.StatusCode);
        var derivative = await candidateFile.Content.ReadAsByteArrayAsync();
        Assert.StartsWith("application/vnd.openxmlformats-officedocument.wordprocessingml.document", candidateFile.Content.Headers.ContentType!.ToString(), StringComparison.OrdinalIgnoreCase);
        using (var archive = new ZipArchive(new MemoryStream(derivative), ZipArchiveMode.Read))
        {
            Assert.True(new HashSet<string>(archive.Entries.Select(x => x.FullName), StringComparer.Ordinal)
                .SetEquals(["[Content_Types].xml", "_rels/.rels", "word/document.xml"]));
            using var reader = new StreamReader(archive.GetEntry("word/document.xml")!.Open());
            var text = await reader.ReadToEndAsync();
            Assert.DoesNotContain("jane.doe@example.invalid", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("linkedin.com", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("+84 912 345 678", text, StringComparison.Ordinal);
            Assert.Contains("PostgreSQL", text, StringComparison.Ordinal);
        }

        using var recruiterFile = await recruiter.GetAsync($"/api/public/resumes/{ownerId}/{versionId}/file");
        Assert.Equal(HttpStatusCode.OK, recruiterFile.StatusCode);
        Assert.Equal(original, await recruiterFile.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task UnreadyDerivativeNeverFallsBackToSourceForAnotherCandidate()
    {
        var (owner, ownerId) = await fixture.User();
        var (otherCandidate, _) = await fixture.User();
        var (recruiter, _) = await fixture.User("Recruiter");
        var original = ContactRichDocx("https://a.com/123456"); // Parser accepts 20 characters; scrubbing leaves too little public text.
        var uploaded = await FoundationFixture.Upload(owner, original, "synthetic-unready.docx");
        var versionId = uploaded.GetProperty("versionId").GetGuid();

        using var publish = await owner.PostAsJsonAsync("/api/resumes/public", new { resumeVersionId = versionId, expectedRowVersion = (long?)null });
        Assert.Equal(HttpStatusCode.OK, publish.StatusCode);
        var selection = await publish.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("not_ready", selection.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, selection.GetProperty("redactionVerifiedAt").ValueKind);

        var visible = await otherCandidate.GetFromJsonAsync<JsonElement[]>("/api/public/resumes?page=1");
        Assert.DoesNotContain(visible!, x => x.GetProperty("candidateId").GetGuid() == ownerId);
        using var candidateFile = await otherCandidate.GetAsync($"/api/public/resumes/{ownerId}/{versionId}/file");
        Assert.Equal(HttpStatusCode.Conflict, candidateFile.StatusCode);
        var problem = await candidateFile.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("redaction_not_ready", problem.GetProperty("title").GetString());

        using var recruiterFile = await recruiter.GetAsync($"/api/public/resumes/{ownerId}/{versionId}/file");
        Assert.Equal(HttpStatusCode.OK, recruiterFile.StatusCode);
        Assert.Equal(original, await recruiterFile.Content.ReadAsByteArrayAsync());
    }

    private static byte[] MinimalPdf(string visibleText, string metadataText)
    {
        static string Escape(string value) => value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("(", "\\(", StringComparison.Ordinal).Replace(")", "\\)", StringComparison.Ordinal);
        var content = $"BT /F1 12 Tf 72 720 Td ({Escape(visibleText)}) Tj ET";
        var stream = $"<< /Length {Encoding.ASCII.GetByteCount(content)} >>\nstream\n{content}\nendstream";
        var objects = new[]
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 5 0 R >> >> /Contents 4 0 R /Annots [6 0 R] >>",
            stream,
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
            "<< /Type /Annot /Subtype /Link /Rect [72 700 300 720] /Border [0 0 0] /A << /S /URI /URI (https://linkedin.com/in/jane) >> >>",
            $"<< /Title ({Escape(metadataText)}) >>"
        };
        var pdf = new StringBuilder("%PDF-1.4\n");
        var offsets = new List<int>();
        foreach (var item in objects)
        {
            offsets.Add(Encoding.ASCII.GetByteCount(pdf.ToString()));
            pdf.Append($"{offsets.Count} 0 obj\n{item}\nendobj\n");
        }
        var xref = Encoding.ASCII.GetByteCount(pdf.ToString());
        pdf.Append($"xref\n0 {objects.Length + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets) pdf.Append($"{offset:0000000000} 00000 n \n");
        pdf.Append($"trailer\n<< /Size {objects.Length + 1} /Root 1 0 R /Info 7 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return Encoding.ASCII.GetBytes(pdf.ToString());
    }

    private static byte[] ContactRichDocx(string text)
    {
        using var stream = new MemoryStream();
        using (var document = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document, true))
        {
            var main = document.AddMainDocumentPart();
            main.Document = new Document(new Body(new Paragraph(new Run(new Text(text)))));
            var image = main.AddImagePart(ImagePartType.Png);
            using (var png = new MemoryStream(Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/p9sAAAAASUVORK5CYII=")))
                image.FeedData(png);
            main.AddHyperlinkRelationship(new Uri("https://linkedin.com/in/jane-doe"), true);
            document.PackageProperties.Title = "Contact jane.doe@example.invalid +84 912 345 678";
            main.Document.Save();
        }
        return stream.ToArray();
    }
}
