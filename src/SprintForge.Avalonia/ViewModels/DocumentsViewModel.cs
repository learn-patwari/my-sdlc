using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SprintForge.Application.Documents;
using System.Collections.ObjectModel;

namespace SprintForge.Avalonia.ViewModels;

public sealed record DocumentItem(
    string DocumentId,
    string Title,
    string Kind,        // "SRS" | "SAD" | "SDD"
    string Version,
    string Status,      // "Draft" | "Approved" | "Published"
    string GeneratedAt,
    string Preview)
{
    public string KindBgHex => Kind switch
    {
        "SRS" => "#4C1D95",
        "SAD" => "#164E63",
        "SDD" => "#1E3A5F",
        _     => "#1A2235"
    };
    public string KindFgHex => Kind switch
    {
        "SRS" => "#C4B5FD",
        "SAD" => "#67E8F9",
        "SDD" => "#93C5FD",
        _     => "#94A3B8"
    };
    public string StatusBgHex => Status switch
    {
        "Approved"  => "#052E16",
        "Published" => "#0C1A0E",
        _           => "#1A2235"
    };
    public string StatusFgHex => Status switch
    {
        "Approved"  => "#22C55E",
        "Published" => "#4ADE80",
        _           => "#94A3B8"
    };
}

public sealed partial class DocumentsViewModel : ObservableObject
{
    private readonly IDocumentVersionStore _store;

    [ObservableProperty] private DocumentItem? _selectedDocument;
    [ObservableProperty] private string _statusMessage = "Select a document to preview.";
    [ObservableProperty] private bool   _isLoading;

    public ObservableCollection<DocumentItem> Documents { get; } = [];

    public DocumentsViewModel(IDocumentVersionStore store)
    {
        _store = store;
        LoadDemoData();
    }

    [RelayCommand]
    private void SelectDocument(DocumentItem? doc)
    {
        SelectedDocument = doc;
        if (doc is not null)
            StatusMessage = $"Showing {doc.Kind} — {doc.Title} ({doc.Version})";
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsLoading     = true;
        StatusMessage = "Loading documents…";
        try
        {
            await Task.Delay(300);
            StatusMessage = "Document store not configured — showing demo data.";
        }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private void ExportDocument()
    {
        if (SelectedDocument is null) { StatusMessage = "Select a document first."; return; }
        StatusMessage = $"Export of '{SelectedDocument.Title}' queued — configure Working Directory in Settings.";
    }

    private void LoadDemoData()
    {
        Documents.Clear();
        Documents.Add(new DocumentItem(
            "RBP-SRS-001",
            "Account Management — SRS",
            "SRS", "v1.0", "Approved",
            "25 Jul 2026",
            "# Software Requirements Specification\n**Project:** RBP — Account Management\n**Version:** v1.0  |  **Status:** Approved\n\n---\n\n" +
            "## 1. Introduction\nThis SRS defines functional and non-functional requirements for the Account Management module.\n\n" +
            "## 2. Functional Requirements\n| FR-001 | User Authentication | High |\n| FR-002 | Account Creation | High |\n| FR-003 | Balance Enquiry | High |\n| FR-004 | Transaction History | Medium |\n\n" +
            "## 3. Non-Functional Requirements\n- Performance: API ≤ 200ms at P95\n- Availability: 99.9% uptime\n- Security: AES-256 at rest, TLS 1.3+"));

        Documents.Add(new DocumentItem(
            "RBP-SAD-001",
            "Retail Banking Platform — SAD",
            "SAD", "v1.0", "Approved",
            "26 Jul 2026",
            "# Solution Architecture Document\n**Platform:** Retail Banking  |  **Version:** v1.0  |  **Status:** Approved\n\n---\n\n" +
            "## 1. Overview\nMicroservices architecture with API Gateway entry point.\n\n" +
            "## 2. Components\n- API Gateway (Spring Cloud)\n- Payment Service (Spring Boot)\n- Account Service (Spring Boot)\n- Notification Service (Spring Boot)\n\n" +
            "## 3. Data Layer\n- PostgreSQL (primary)\n- Redis (cache)\n- Kafka (event streaming)"));

        Documents.Add(new DocumentItem(
            "RBP-SDD-001",
            "Payment Service — SDD",
            "SDD", "v0.2", "Draft",
            "28 Jul 2026",
            "# Software Design Document\n**Service:** Payment Service  |  **Version:** v0.2 Draft\n\n---\n\n" +
            "## 1. Overview\nLayered architecture: Controller → Service → Repository.\n\n" +
            "## 2. Design Details\n- REST endpoints via Spring Boot controllers\n- Business rules in Service layer\n- JPA repositories with PostgreSQL\n\n" +
            "## 3. Sequence Diagrams\nSee draw.io export in generated files.\n\n" +
            "## 4. Error Handling\nAll exceptions mapped to RFC 7807 ProblemDetails."));

        Documents.Add(new DocumentItem(
            "RBP-SRS-002",
            "Payment Service — SRS",
            "SRS", "v0.1", "Draft",
            "29 Jul 2026",
            "# Software Requirements Specification\n**Service:** Payment Service  |  **Version:** v0.1 Draft\n\n---\n\n" +
            "## 1. Scope\nPayment processing, fund transfers, and payment event publishing.\n\n" +
            "## 2. Functional Requirements\n| PY-001 | Initiate Fund Transfer | High |\n| PY-002 | Validate Account Balance | High |\n| PY-003 | Publish Payment Event | Medium |\n| PY-004 | Idempotent Retry Handling | High |"));

        SelectedDocument = Documents[0];
        StatusMessage    = $"Loaded {Documents.Count} documents.";
    }
}
