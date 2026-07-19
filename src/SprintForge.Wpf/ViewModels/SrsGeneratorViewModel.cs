using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SprintForge.Application.Documents;
using SprintForge.Application.Approval;
using SprintForge.Domain.Documents;
using SprintForge.Domain.Approvals;
using System.Collections.ObjectModel;
using System.Text;

namespace SprintForge.Wpf.ViewModels;

public sealed partial class SrsGeneratorViewModel : ObservableObject
{
    private readonly ISrsService _srsService;
    private readonly IApprovalGate _approvalGate;

    [ObservableProperty]
    private string _inputText =
        "# Account Management Requirements\n\n" +
        "## FR-01: User Authentication\n" +
        "The system shall allow users to authenticate via username and password.\n\n" +
        "## FR-02: Account Creation\n" +
        "The system shall allow bank staff to create new customer accounts.\n\n" +
        "## FR-03: Balance Enquiry\n" +
        "Users shall be able to view current account balance in real time.\n\n" +
        "## FR-04: Transaction History\n" +
        "Users shall be able to view the last 90 days of transactions.";

    [ObservableProperty]
    private string _generatedPreview = string.Empty;

    [ObservableProperty]
    private ObservableCollection<DiffLine> _diffLines = [];

    [ObservableProperty]
    private bool _isGenerating;

    [ObservableProperty]
    private string _statusMessage = "Enter requirements and click Generate.";

    [ObservableProperty]
    private string _jiraProjectKey = "RBP";

    [ObservableProperty]
    private DocumentDraft? _currentDraft;

    [ObservableProperty]
    private bool _canSubmitForApproval;

    public string ReviewerName { get; } = Environment.UserName;

    public SrsGeneratorViewModel(ISrsService srsService, IApprovalGate approvalGate)
    {
        _srsService = srsService;
        _approvalGate = approvalGate;
    }

    partial void OnInputTextChanged(string value) =>
        StatusMessage = $"{CountWords(value)} words";

    partial void OnCurrentDraftChanged(DocumentDraft? value) =>
        CanSubmitForApproval = value is not null;

    [RelayCommand(CanExecute = nameof(CanGenerate))]
    private async Task GenerateAsync()
    {
        if (string.IsNullOrWhiteSpace(InputText)) return;

        IsGenerating = true;
        StatusMessage = "Generating SRS via AI…";
        GeneratedPreview = string.Empty;
        DiffLines.Clear();

        try
        {
            var request = new SrsGenerationRequest
            {
                DocumentId = $"SRS-{DateTime.Now:yyyyMMdd-HHmmss}",
                JiraProjectKey = string.IsNullOrWhiteSpace(JiraProjectKey) ? "PROJ" : JiraProjectKey.Trim(),
                InputMarkdown = InputText,
                CorrelationId = Guid.NewGuid().ToString("N")
            };

            var result = await _srsService.GenerateDraftAsync(request);

            if (result.IsSuccess && result.Value is not null)
            {
                CurrentDraft = result.Value;
                GeneratedPreview = result.Value.ContentMarkdown;
                BuildDiffFromContent(result.Value.ContentMarkdown);
                StatusMessage = $"Generated — {CountWords(result.Value.ContentMarkdown)} words. Review and submit for approval.";
            }
            else
            {
                StatusMessage = $"Generation failed: {result.Error}";
            }
        }
        catch (NotImplementedException)
        {
            // AI provider not yet configured; show a representative demo
            var demo = BuildDemoPreview();
            GeneratedPreview = demo;
            BuildDemoDiff();
            StatusMessage = "AI provider not configured — showing demo preview. Configure in Settings → Integrations.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsGenerating = false;
        }
    }

    private bool CanGenerate() => !IsGenerating && !string.IsNullOrWhiteSpace(InputText);

    [RelayCommand]
    private async Task SubmitForApprovalAsync()
    {
        if (CurrentDraft is null)
        {
            StatusMessage = "Generate an SRS first.";
            return;
        }

        try
        {
            var result = await _srsService.SubmitForApprovalAsync(
                CurrentDraft,
                CurrentDraft.CorrelationId,
                Environment.UserName);

            if (result.IsSuccess)
            {
                StatusMessage = $"Queued in Approvals Center (ID: {result.Value!.ApprovalId[..8]}…). Navigate to Approvals Center to review.";
                CurrentDraft = null;
            }
            else
            {
                StatusMessage = $"Submit failed: {result.Error}";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
    }

    [RelayCommand]
    private void Clear()
    {
        InputText = string.Empty;
        GeneratedPreview = string.Empty;
        DiffLines.Clear();
        CurrentDraft = null;
        StatusMessage = "Cleared.";
    }

    private void BuildDiffFromContent(string markdown)
    {
        var lines = markdown.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var reqLines = lines.Where(l => l.StartsWith("## FR") || l.StartsWith("| FR")).Take(8);
        int i = 1;
        foreach (var line in reqLines)
        {
            DiffLines.Add(new DiffLine($"FR-{i:D3}", line.TrimStart('#', '|', ' ').Trim(), "Newly generated requirement", "Added"));
            i++;
        }
    }

    private void BuildDemoDiff()
    {
        DiffLines.Clear();
        DiffLines.Add(new DiffLine("FR-001", "User Authentication", "System authenticates users via username/password", "Added"));
        DiffLines.Add(new DiffLine("FR-002", "Account Creation", "Staff can create new customer accounts with KYC verification", "Added"));
        DiffLines.Add(new DiffLine("FR-003", "Balance Enquiry", "Real-time balance retrieval via core banking integration", "Added"));
        DiffLines.Add(new DiffLine("FR-004", "Transaction History", "90-day transaction history with pagination and export", "Modified"));
        DiffLines.Add(new DiffLine("FR-005", "Legacy Password Reset", "Password reset via SMS OTP", "Removed"));
    }

    private string BuildDemoPreview()
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Software Requirements Specification");
        sb.AppendLine($"**Project:** {JiraProjectKey}  |  **Version:** v0.1 Draft  |  **Date:** {DateTime.Today:dd MMM yyyy}");
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("## 1. Introduction");
        sb.AppendLine("This SRS defines functional and non-functional requirements for the Account Management module of the Retail Banking Platform.");
        sb.AppendLine();
        sb.AppendLine("## 2. Functional Requirements");
        sb.AppendLine();
        sb.AppendLine("| Req ID | Requirement | Priority | Status |");
        sb.AppendLine("|--------|-------------|----------|--------|");
        sb.AppendLine("| FR-001 | User Authentication | High | Draft |");
        sb.AppendLine("| FR-002 | Account Creation | High | Draft |");
        sb.AppendLine("| FR-003 | Balance Enquiry | High | Draft |");
        sb.AppendLine("| FR-004 | Transaction History | Medium | Draft |");
        sb.AppendLine();
        sb.AppendLine("## 3. Non-Functional Requirements");
        sb.AppendLine("- **Performance:** API response ≤ 200ms at P95 under 1,000 concurrent users.");
        sb.AppendLine("- **Availability:** 99.9% uptime excluding planned maintenance windows.");
        sb.AppendLine("- **Security:** All data at rest encrypted (AES-256); in-transit via TLS 1.3+.");
        sb.AppendLine();
        sb.AppendLine("## 4. Assumptions");
        sb.AppendLine("- Core banking system exposes REST APIs for account and transaction data.");
        sb.AppendLine("- Identity provider (SSO) is available and integrated.");
        return sb.ToString();
    }

    private static int CountWords(string text) =>
        string.IsNullOrWhiteSpace(text) ? 0 : text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
}
