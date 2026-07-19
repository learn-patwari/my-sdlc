using Markdig;

namespace SprintForge.Infrastructure.Documents.Exporters;

/// <summary>Converts Markdown content to a self-contained HTML file using Markdig.</summary>
public static class HtmlExporter
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .UseEmojiAndSmiley()
        .Build();

    public static async Task ExportAsync(string markdownContent, string documentTitle, string outputPath, CancellationToken ct = default)
    {
        var body = Markdown.ToHtml(markdownContent, Pipeline);
        var html = WrapInPage(documentTitle, body);
        await File.WriteAllTextAsync(outputPath, html, System.Text.Encoding.UTF8, ct).ConfigureAwait(false);
    }

    private static string WrapInPage(string title, string body)
    {
        var encodedTitle = System.Net.WebUtility.HtmlEncode(title);
        return $$"""
            <!DOCTYPE html>
            <html lang="en">
            <head>
              <meta charset="UTF-8" />
              <meta name="viewport" content="width=device-width, initial-scale=1.0" />
              <title>{{encodedTitle}}</title>
              <style>
                :root { --bg: #080c18; --surface: #0d1117; --card: #111827; --border: #1e2d45;
                        --accent: #7c3aed; --text: #f1f5f9; --muted: #94a3b8; }
                body { font-family: 'Segoe UI', system-ui, sans-serif; background: var(--bg);
                       color: var(--text); max-width: 960px; margin: 0 auto; padding: 2rem; line-height: 1.6; }
                h1 { color: var(--accent); font-size: 1.8rem; border-bottom: 1px solid var(--border); padding-bottom: 0.5rem; }
                h2 { color: var(--text); font-size: 1.3rem; margin-top: 2rem; }
                h3 { color: var(--muted); font-size: 1.1rem; }
                table { border-collapse: collapse; width: 100%; margin: 1rem 0; }
                th, td { border: 1px solid var(--border); padding: 0.5rem 0.75rem; text-align: left; }
                th { background: var(--card); color: var(--accent); }
                tr:nth-child(even) { background: var(--surface); }
                code { background: var(--card); padding: 0.1rem 0.3rem; border-radius: 3px; font-size: 0.9em; }
                pre { background: var(--card); padding: 1rem; border-radius: 6px; overflow-x: auto; }
              </style>
            </head>
            <body>
            {{body}}
            </body>
            </html>
            """;
    }
}
