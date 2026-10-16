using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace CareerGap.Api.Files;

public sealed class RedactedResumeBuilder
{
    public const string MethodVersion = "text-only-docx-contact-scrub-v1";
    private static readonly Regex Url = new(
        @"(?ix)\b(?:https?://|www\.|mailto:)[^\s<>]+|\b(?:linkedin|github|gitlab|behance|portfolio)\.com(?:/[^\s<>]*)?|\b[a-z0-9-]+\.(?:com|net|org|io|vn|dev|edu|gov|co)(?:/[^\s<>]*)?",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex Email = new(
        @"(?ix)\b[a-z0-9.!#$%&'*+/=?^_`{|}~-]+\s*(?:@|\[at\]|\(at\)|\sat\s)\s*[a-z0-9-]+(?:\s*(?:\.|\[dot\]|\(dot\)|\sdot\s)\s*[a-z0-9-]+)*\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex Phone = new(
        @"(?<![\p{L}\d])(?:\+|00)?(?:\d[\s().-]*){8,15}(?!\d)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex Whitespace = new(@"[\t\x0B\f ]{2,}", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private const string Mask = "[đã ẩn liên hệ]";

    public byte[]? Build(ParsedDocument source, CancellationToken ct)
    {
        var text = source.Segments.Select(x => Scrub(x.Text)).Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
        if (text.Sum(x => x.Length) < 20) return null;
        ct.ThrowIfCancellationRequested();

        using var stream = new MemoryStream();
        using (var document = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document, true))
        {
            var main = document.AddMainDocumentPart();
            var body = new Body();
            body.Append(new Paragraph(new Run(new Text("CV — bản đã ẩn thông tin liên hệ") { Space = SpaceProcessingModeValues.Preserve })));
            foreach (var segment in text)
            {
                ct.ThrowIfCancellationRequested();
                body.Append(new Paragraph(new Run(new Text(segment) { Space = SpaceProcessingModeValues.Preserve })));
            }
            main.Document = new Document(body);
            main.Document.Save();
        }
        var result = stream.ToArray();
        return Verify(result, ct) ? result : null;
    }

    public bool Verify(byte[] bytes, CancellationToken ct)
    {
        try
        {
            using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
            var names = archive.Entries.Select(x => x.FullName).ToHashSet(StringComparer.Ordinal);
            if (!names.SetEquals(["[Content_Types].xml", "_rels/.rels", "word/document.xml"])) return false;
            if (archive.Entries.Any(x => x.Length > 1024 * 1024)) return false;

            var documentEntry = archive.GetEntry("word/document.xml");
            if (documentEntry is null) return false;
            XDocument xml;
            using (var input = documentEntry.Open()) xml = XDocument.Load(input, LoadOptions.None);
            var forbiddenElements = new HashSet<string>(StringComparer.Ordinal)
                { "hyperlink", "drawing", "pict", "object", "instrText", "fldSimple", "altChunk" };
            if (xml.Descendants().Any(x => forbiddenElements.Contains(x.Name.LocalName))) return false;

            var relationshipsEntry = archive.GetEntry("_rels/.rels");
            if (relationshipsEntry is null) return false;
            using (var input = relationshipsEntry.Open())
            {
                var relationships = XDocument.Load(input, LoadOptions.None);
                if (relationships.Descendants().Any(x => string.Equals((string?)x.Attribute("TargetMode"), "External", StringComparison.OrdinalIgnoreCase))) return false;
            }

            ct.ThrowIfCancellationRequested();
            var parsed = new DocumentParser().Parse(bytes, "redacted.docx", ct);
            return parsed.Segments.Count > 0 && parsed.Segments.All(x => !ContainsContact(x.Text));
        }
        catch (Exception ex) when (ex is InvalidDataException or System.Xml.XmlException or DocumentError or ArgumentException or IOException)
        {
            return false;
        }
    }

    private static string Scrub(string text)
    {
        var cleaned = Email.Replace(text, Mask);
        cleaned = Url.Replace(cleaned, Mask);
        cleaned = Phone.Replace(cleaned, Mask);
        return Whitespace.Replace(cleaned, " ").Trim();
    }

    private static bool ContainsContact(string text) => Url.IsMatch(text) || Email.IsMatch(text) || Phone.IsMatch(text);
}
