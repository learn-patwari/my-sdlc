# 9. Document Generation Engine

Covers specification output section **§14 Document Generation Engine Design** — the pipeline that converts AI-generated content into versioned, hashed, exportable artifacts.

---

## 9.1 Overview

The document engine sits between generators (SRS, SAD, SDD, Test) and the file system. Its responsibilities:

1. Accept structured content (markdown, diagram XML, code, tables).
2. Render into target formats (DOCX, PDF, Markdown, HTML, Draw.io XML, PNG).
3. Apply user-supplied templates (layout, styles, headers, footers, company branding).
4. Compute a SHA-256 hash of every output file.
5. Write to the versioned directory under Working Directory.
6. Record the file path + hash in the audit event.
7. Maintain a `VersionManifest` for each document ID.

---

## 9.2 Core interfaces

```csharp
interface IDocumentGenerator {
  Task<GeneratedDocument> Generate(DocumentGenerationRequest request);
}

interface IDocumentVersionStore {
  Task<DocumentVersion>         Save(GeneratedDocument doc, string auditId);
  Task<DocumentVersion>         GetVersion(string documentId, int version, ExportFormat format);
  Task<VersionManifest>         GetManifest(string documentId);
  Task<IList<DocumentVersion>>  ListVersions(string documentId);
}

interface IDocumentExporter {
  Task<byte[]> Export(DocumentVersion version, ExportFormat targetFormat);
  IList<ExportFormat> SupportedFormats(ExportFormat sourceFormat);
}

record DocumentGenerationRequest {
  public string DocumentId { get; set; }       // e.g. "SRS:PROJ-1234"
  public DocumentKind Kind { get; set; }        // SRS, SAD, SDD, TestSuite, DiagramXml
  public string Content { get; set; }           // markdown / draw.io XML / code
  public string TemplateName { get; set; }      // from Templates/
  public ExportFormat PrimaryFormat { get; set; }   // DOCX by default
  public ExportFormat[] AdditionalFormats { get; set; }
  public string AuditId { get; set; }
}

record GeneratedDocument {
  public string DocumentId { get; set; }
  public DocumentKind Kind { get; set; }
  public Dictionary<ExportFormat, byte[]> Outputs { get; set; }
  public Dictionary<ExportFormat, string> FilePaths { get; set; }
  public Dictionary<ExportFormat, string> Sha256Hashes { get; set; }
}
```

---

## 9.3 Template engine

Templates are stored in `<WorkingDirectory>/Templates/` (or the configured `documents.templateDir`). Templates are format-specific:

| Format | Template type | Library |
|---|---|---|
| DOCX | `.docx` Word template file (`.dotx`) with content controls / bookmarks | Open XML SDK |
| PDF | HTML template (CSS + layout) converted to PDF | QuestPDF or wkhtmltopdf |
| Markdown | Liquid `.md` template with section stubs | Fluid (Liquid renderer) |
| HTML | Liquid `.html` template | Fluid |
| Draw.io | Base XML template with placeholder shapes | Native mxGraph XML writer |

### DOCX template approach (Open XML SDK)

Content controls in the `.dotx` file are tagged with identifiers (e.g., `SRS_FUNCTIONAL_REQUIREMENTS`). The generator maps AI-generated sections to these tags:

```csharp
class DocxDocumentGenerator {
  public async Task<byte[]> Generate(string templatePath, Dictionary<string, string> sections) {
    using var stream = File.OpenRead(templatePath);
    using var doc = WordprocessingDocument.CreateFromTemplate(stream);

    foreach (var (tag, content) in sections) {
      var control = doc.FindSdtByTag(tag);
      if (control != null) {
        control.SetPlainText(content);  // or SetMarkdown(content) with converter
      }
    }

    doc.Save();
    return doc.ToByteArray();
  }
}
```

Custom styles (fonts, colors, heading styles) come from the template `.dotx` itself — the generator never sets styles in code, so changing the template file changes all future documents without a code change.

### PDF generation

Primary: **QuestPDF** layout engine (Community license — verify commercial use requirements):

```csharp
class PdfDocumentGenerator {
  public byte[] Generate(string htmlContent, string templateCss) {
    return Document.Create(container => {
      container.Page(page => {
        page.DefaultTextStyle(x => x.FontFamily("Segoe UI").FontSize(11));
        page.Content().Text(htmlContent);  // or rich content via columns/tables
      });
    }).GeneratePdf();
  }
}
```

Fallback: convert HTML to PDF via Playwright headless print (Chromium is pre-installed in the desktop environment for this purpose):

```csharp
class PlaywrightPdfFallback {
  public async Task<byte[]> ConvertHtmlToPdf(string htmlPath) {
    using var playwright = await Playwright.CreateAsync();
    await using var browser = await playwright.Chromium.LaunchAsync();
    var page = await browser.NewPageAsync();
    await page.GotoAsync($"file:///{htmlPath}");
    return await page.PdfAsync(new PagePdfOptions { Format = "A4" });
  }
}
```

### Draw.io XML generation

Diagrams are generated as native mxGraph XML. The `DrawioWriter` class:

```csharp
class DrawioWriter {
  private readonly XDocument _doc;

  public DrawioWriter() {
    _doc = XDocument.Parse(@"
      <mxGraphModel><root>
        <mxCell id=""0""/><mxCell id=""1"" parent=""0""/>
      </root></mxGraphModel>");
  }

  public void AddComponent(string id, string label, ComponentStyle style, double x, double y) {
    var cell = new XElement("mxCell",
      new XAttribute("id", id),
      new XAttribute("value", label),
      new XAttribute("style", StyleToString(style)),
      new XAttribute("vertex", "1"),
      new XAttribute("parent", "1"),
      new XElement("mxGeometry",
        new XAttribute("x", x), new XAttribute("y", y),
        new XAttribute("width", 120), new XAttribute("height", 60),
        new XAttribute("as", "geometry")));
    _doc.Root!.Element("root")!.Add(cell);
  }

  public void AddEdge(string id, string source, string target, string label = "") { /* ... */ }

  public string ToXml() => _doc.ToString();
}
```

PNG preview: if bundled drawio CLI is available at `config.drawio.cliPath`, invoke it:

```csharp
class DrawioPngRenderer {
  public async Task<byte[]?> RenderPng(string drawioXmlPath) {
    if (!File.Exists(_cliPath)) return null;  // graceful: no PNG, not an error
    
    var outputPath = Path.ChangeExtension(drawioXmlPath, ".png");
    var result = await Cli.Wrap(_cliPath)
      .WithArguments($"--export --format png --output \"{outputPath}\" \"{drawioXmlPath}\"")
      .ExecuteAsync();
    
    return result.ExitCode == 0 ? await File.ReadAllBytesAsync(outputPath) : null;
  }
}
```

---

## 9.4 Versioning

Every call to `IDocumentVersionStore.Save()` increments the version and writes a manifest entry:

### Directory layout
```
Documents/
└── SRS/
    └── PROJ-1234/
        ├── manifest.json
        ├── v001/
        │   ├── SRS-PROJ-1234.docx
        │   ├── SRS-PROJ-1234.pdf
        │   └── SRS-PROJ-1234.md
        ├── v002/
        │   └── ...
        └── v003/     ← latest
```

### Manifest format
```json
{
  "documentId": "SRS:PROJ-1234",
  "kind": "SRS",
  "versions": [
    {
      "version": 1,
      "createdUtc": "2024-12-15T09:00:00Z",
      "auditId": "01ARZ3NDEKTSV4RRFFQ69G5FAV",
      "files": [
        { "format": "DOCX", "path": "v001/SRS-PROJ-1234.docx", "sha256": "abc123..." },
        { "format": "PDF",  "path": "v001/SRS-PROJ-1234.pdf",  "sha256": "def456..." },
        { "format": "MD",   "path": "v001/SRS-PROJ-1234.md",   "sha256": "ghi789..." }
      ],
      "note": "Initial generation"
    },
    {
      "version": 2,
      "createdUtc": "2024-12-16T10:30:00Z",
      "auditId": "01ARZ3NDEKTSV4RRFFQ69G5FAX",
      "files": [ /* ... */ ],
      "note": "Revised after stakeholder review",
      "restoredFrom": null
    }
  ]
}
```

### Integrity check

On document access, the engine re-hashes the file and compares to the manifest. If they differ, an integrity warning is shown and the mismatch is recorded in the audit log.

---

## 9.5 Version comparison

The "Compare v1 vs v2" feature in the UI uses a content-diff approach:

| Format | Diff method |
|---|---|
| Markdown / text | `DiffPlex` library — line + word diffs, produces annotated HTML |
| DOCX | Convert both versions to plain text, then diff |
| Code (test files) | `DiffPlex` with syntax highlighting |
| Draw.io XML | Structural diff on XML elements (components added/removed/moved) |

Diff output is rendered in the document preview pane with:
- Green background = additions.
- Red background = deletions.
- Section-level collapse if unchanged (only show changed sections by default).

---

## 9.6 Export formats

Export converts a stored version to a different format on demand:

| Source → Target | Method |
|---|---|
| MD → DOCX | Markdig → HTML → Open XML SDK |
| MD → PDF | Markdig → HTML → QuestPDF |
| MD → HTML | Markdig |
| DOCX → PDF | `LibreOffice` headless conversion (if available), else Playwright print of HTML export |
| Draw.io XML → PNG | drawio CLI (if available) |

Export jobs are tracked and audited (even on-demand exports).

---

## 9.7 Document + Jira linking

After a document is approved and saved, the engine optionally attaches it to the related Jira issue:

```csharp
// In the approval executor, after save:
if (request.AttachToJira && request.JiraIssueKey != null) {
  await _jiraClient.AttachFile(
    request.JiraIssueKey,
    content,
    fileName,
    "application/vnd.openxmlformats-officedocument.wordprocessingml.document");
}
```

This is a separate write operation — queued as its own approval item in the Approvals Center, not bundled with the document save.
