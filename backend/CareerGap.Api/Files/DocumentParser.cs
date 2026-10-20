using System.Diagnostics;
using System.IO.Compression;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.PageSegmenter;
using UglyToad.PdfPig.DocumentLayoutAnalysis.ReadingOrderDetector;
using UglyToad.PdfPig.DocumentLayoutAnalysis.WordExtractor;

namespace CareerGap.Api.Files;

public sealed record SourceSegment(string Id, string Text, int? Page = null, int? Paragraph = null, string? Region = null);
public sealed record ParsedDocument(string MimeType, IReadOnlyList<SourceSegment> Segments);
public sealed class DocumentError(string code) : Exception(code) { public string Code { get; } = code; }
public sealed class DocumentParser
{
    public const int MaxBytes = 4 * 1024 * 1024;
    public const string Version = "pdfpig-0.1.16-openxml-3.5.1-v2-isolated";
    public ParsedDocument Parse(byte[] bytes, string name, CancellationToken ct) => ParserProcess.Parse(bytes, name, ct);
    internal ParsedDocument ParseCore(byte[] bytes, string name, CancellationToken ct)
    {
        if (bytes.Length is 0 or > MaxBytes) throw new DocumentError("file_size");
        var watch = Stopwatch.StartNew();
        var segments = new List<SourceSegment>();
        void Add(string id, string text, int? page = null, int? paragraph = null, string? region = null)
        {
            ct.ThrowIfCancellationRequested();
            if (watch.Elapsed > TimeSpan.FromSeconds(10)) throw new DocumentError("parse_timeout");
            if (segments.Count >= 500 || segments.Sum(x => x.Text.Length) + text.Length > 24000) throw new DocumentError("text_limit");
            if (!string.IsNullOrWhiteSpace(text)) segments.Add(new(id, text.Trim(), page, paragraph, region));
        }
        try
        {
            switch (Path.GetExtension(name).ToLowerInvariant())
            {
                case ".pdf":
                    if (!bytes.AsSpan().StartsWith("%PDF-"u8)) throw new DocumentError("file_format");
                    using (var pdf = PdfDocument.Open(bytes))
                    {
                        if (pdf.NumberOfPages > 20) throw new DocumentError("page_limit");
                        foreach (var page in pdf.GetPages())
                        {
                            ct.ThrowIfCancellationRequested();
                            var words = NearestNeighbourWordExtractor.Instance.GetWords(page.Letters);
                            var blocks = DocstrumBoundingBoxes.Instance.GetBlocks(words);
                            var index = 0;
                            foreach (var block in UnsupervisedReadingOrderDetector.Instance.Get(blocks)) Add($"page-{page.Number}-block-{++index}", block.Text, page.Number, null, "body");
                        }
                    }
                    if (segments.Sum(x => x.Text.Length) < 20) throw new DocumentError("no_text_ocr_unsupported");
                    return new("application/pdf", segments);
                case ".docx":
                    using (var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read))
                    {
                        if (zip.Entries.Count > 512 || zip.Entries.Sum(x => x.Length) > 20 * 1024 * 1024) throw new DocumentError("docx_expansion_limit");
                        if (zip.GetEntry("[Content_Types].xml") is null || zip.GetEntry("word/document.xml") is null || zip.Entries.Any(x => x.FullName.EndsWith("vbaProject.bin", StringComparison.OrdinalIgnoreCase))) throw new DocumentError("file_format");
                    }
                    using (var doc = WordprocessingDocument.Open(new MemoryStream(bytes), false, new OpenSettings { MaxCharactersInPart = 1024 * 1024 }))
                    {
                        if (doc.DocumentType != DocumentFormat.OpenXml.WordprocessingDocumentType.Document) throw new DocumentError("file_format");
                        var body = doc.MainDocumentPart?.Document?.Body ?? throw new DocumentError("file_format");
                        var index = 0;
                        foreach (var paragraph in body.Descendants<Paragraph>()) Add($"paragraph-{++index}", paragraph.InnerText, null, index, "body");
                        var partIndex = 0;
                        foreach (var header in doc.MainDocumentPart!.HeaderParts)
                        { var i = 0; foreach (var paragraph in header.Header?.Descendants<Paragraph>() ?? []) Add($"header-{partIndex}-paragraph-{++i}", paragraph.InnerText, null, i, "header"); partIndex++; }
                        partIndex = 0;
                        foreach (var footer in doc.MainDocumentPart.FooterParts)
                        { var i = 0; foreach (var paragraph in footer.Footer?.Descendants<Paragraph>() ?? []) Add($"footer-{partIndex}-paragraph-{++i}", paragraph.InnerText, null, i, "footer"); partIndex++; }
                    }
                    if (segments.Sum(x => x.Text.Length) < 20) throw new DocumentError("no_text");
                    return new("application/vnd.openxmlformats-officedocument.wordprocessingml.document", segments);
                default: throw new DocumentError("file_format");
            }
        }
        catch (DocumentError) { throw; }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is not OutOfMemoryException) { throw new DocumentError("file_unreadable"); }
    }
}
