using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace SprintForge.Infrastructure.Documents.Exporters;

/// <summary>
///   Converts Markdown content to DOCX using the Open XML SDK.
///   Parses headings and paragraphs from Markdown; tables are rendered as plain text
///   in this phase — full table support is Phase 6.
/// </summary>
public static class DocxExporter
{
    public static void Export(string markdownContent, string documentTitle, string outputPath)
    {
        using var doc = WordprocessingDocument.Create(outputPath, WordprocessingDocumentType.Document);
        var mainPart = doc.AddMainDocumentPart();
        mainPart.Document = new Document();
        var body = new Body();

        // Title paragraph
        body.AppendChild(MakeParagraph(documentTitle, fontSize: 32, bold: true, spacingAfter: 240));

        foreach (var rawLine in markdownContent.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');

            if (line.StartsWith("### ", StringComparison.Ordinal))
                body.AppendChild(MakeParagraph(line[4..], fontSize: 24, bold: true, spacingBefore: 160, spacingAfter: 80));
            else if (line.StartsWith("## ", StringComparison.Ordinal))
                body.AppendChild(MakeParagraph(line[3..], fontSize: 26, bold: true, spacingBefore: 200, spacingAfter: 100));
            else if (line.StartsWith("# ", StringComparison.Ordinal))
                body.AppendChild(MakeParagraph(line[2..], fontSize: 28, bold: true, spacingBefore: 240, spacingAfter: 120));
            else if (line.StartsWith("|", StringComparison.Ordinal) && line.EndsWith("|", StringComparison.Ordinal))
                body.AppendChild(MakeTableRow(line));
            else if (!string.IsNullOrWhiteSpace(line) && !line.StartsWith("---", StringComparison.Ordinal))
                body.AppendChild(MakeParagraph(StripInlineMarkdown(line), fontSize: 22, spacingAfter: 100));
        }

        mainPart.Document.AppendChild(body);
        mainPart.Document.Save();
    }

    private static Paragraph MakeParagraph(
        string text,
        int fontSize = 22,
        bool bold = false,
        int spacingBefore = 0,
        int spacingAfter = 0)
    {
        var runProps = new RunProperties();
        if (bold) runProps.AppendChild(new Bold());
        runProps.AppendChild(new FontSize { Val = fontSize.ToString() });
        runProps.AppendChild(new RunFonts { Ascii = "Segoe UI", HighAnsi = "Segoe UI" });

        var paraProps = new ParagraphProperties();
        if (spacingBefore > 0 || spacingAfter > 0)
            paraProps.AppendChild(new SpacingBetweenLines
            {
                Before = spacingBefore > 0 ? spacingBefore.ToString() : null,
                After = spacingAfter > 0 ? spacingAfter.ToString() : null
            });

        return new Paragraph(paraProps, new Run(runProps, new Text(text) { Space = SpaceProcessingModeValues.Preserve }));
    }

    private static Paragraph MakeTableRow(string line)
    {
        // Render table rows as indented monospace text (full DOCX table support is Phase 6)
        var cells = line.Split('|', StringSplitOptions.RemoveEmptyEntries);
        var text = string.Join("  |  ", cells.Select(c => c.Trim()));
        var runProps = new RunProperties();
        runProps.AppendChild(new RunFonts { Ascii = "Cascadia Code", HighAnsi = "Cascadia Code" });
        runProps.AppendChild(new FontSize { Val = "18" });
        return new Paragraph(
            new ParagraphProperties(new Indentation { Left = "720" }),
            new Run(runProps, new Text(text) { Space = SpaceProcessingModeValues.Preserve }));
    }

    private static string StripInlineMarkdown(string line)
    {
        // Strip bold (**text** or __text__) and italic (*text* or _text_)
        line = System.Text.RegularExpressions.Regex.Replace(line, @"\*\*(.+?)\*\*", "$1");
        line = System.Text.RegularExpressions.Regex.Replace(line, @"__(.+?)__", "$1");
        line = System.Text.RegularExpressions.Regex.Replace(line, @"\*(.+?)\*", "$1");
        line = System.Text.RegularExpressions.Regex.Replace(line, @"_(.+?)_", "$1");
        line = System.Text.RegularExpressions.Regex.Replace(line, @"`(.+?)`", "$1");
        // Strip leading list markers
        line = System.Text.RegularExpressions.Regex.Replace(line, @"^[-*+]\s+", "• ");
        line = System.Text.RegularExpressions.Regex.Replace(line, @"^\d+\.\s+", m => m.Value);
        return line;
    }
}
