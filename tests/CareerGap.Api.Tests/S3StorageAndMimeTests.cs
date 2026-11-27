using System.IO.Compression;
using System.Text;
using CareerGap.Api.Files;
using Xunit;

namespace CareerGap.Api.Tests;

public sealed class S3StorageAndMimeTests
{
    [Fact]
    public void NormalizeS3Endpoint_StripsBucketPathAndInfersR2AutoRegion()
    {
        var (url, region) = S3FileStorage.NormalizeS3Endpoint(
            "  https://01af696546bb46847cddb8cfdcd2af2e.r2.cloudflarestorage.com/careergapai-private  ",
            " careergapai-private ",
            null);

        Assert.Equal("https://01af696546bb46847cddb8cfdcd2af2e.r2.cloudflarestorage.com/", url.ToString());
        Assert.Equal("auto", region);
    }

    [Fact]
    public void NormalizeS3Endpoint_UsesUsEast1ForNonR2AndRespectsExplicitRegion()
    {
        var (url1, region1) = S3FileStorage.NormalizeS3Endpoint("https://s3.amazonaws.com", "my-bucket", null);
        Assert.Equal("https://s3.amazonaws.com/", url1.ToString());
        Assert.Equal("us-east-1", region1);

        var (_, region2) = S3FileStorage.NormalizeS3Endpoint(
            "https://01af696546bb46847cddb8cfdcd2af2e.r2.cloudflarestorage.com",
            "careergapai-private",
            " weur ");
        Assert.Equal("weur", region2);
    }

    [Fact]
    public void NormalizeS3Endpoint_RejectsHttp()
    {
        Assert.Throws<InvalidOperationException>(() =>
            S3FileStorage.NormalizeS3Endpoint("http://localhost:9000", "bucket", null));
    }

    [Theory]
    [InlineData(null, "application/pdf", true)]
    [InlineData("", "application/pdf", true)]
    [InlineData("application/octet-stream", "application/pdf", true)]
    [InlineData("application/pdf", "application/pdf", true)]
    [InlineData("application/x-pdf", "application/pdf", true)]
    [InlineData("application/vnd.openxmlformats-officedocument.wordprocessingml.document", "application/vnd.openxmlformats-officedocument.wordprocessingml.document", true)]
    [InlineData("application/zip", "application/vnd.openxmlformats-officedocument.wordprocessingml.document", true)]
    [InlineData("application/x-zip-compressed", "application/vnd.openxmlformats-officedocument.wordprocessingml.document", true)]
    [InlineData("image/png", "application/pdf", false)]
    [InlineData("application/pdf", "application/vnd.openxmlformats-officedocument.wordprocessingml.document", false)]
    public void IsCompatibleUploadMime_HandlesBrowserAliases(string? contentType, string parsedMime, bool expected)
    {
        Assert.Equal(expected, FileEndpoints.IsCompatibleUploadMime(contentType, parsedMime));
    }

    [Fact]
    public void DocumentParser_ParsesValidDocxInMemory()
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteZipEntry(zip, "[Content_Types].xml",
                """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/></Types>""");
            WriteZipEntry(zip, "_rels/.rels",
                """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/></Relationships>""");
            WriteZipEntry(zip, "word/document.xml",
                """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"><w:body><w:p><w:r><w:t>Senior Backend Engineer with 5 years C# and PostgreSQL experience.</w:t></w:r></w:p></w:body></w:document>""");
        }

        var parsed = new DocumentParser().Parse(ms.ToArray(), "resume.docx", CancellationToken.None);
        Assert.Equal("application/vnd.openxmlformats-officedocument.wordprocessingml.document", parsed.MimeType);
        Assert.Contains(parsed.Segments, s => s.Text.Contains("Senior Backend Engineer"));
    }

    private static void WriteZipEntry(ZipArchive zip, string path, string content)
    {
        var entry = zip.CreateEntry(path);
        using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
        writer.Write(content);
    }
}
