using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SprintForge.Application.Approval;
using SprintForge.Domain.Approvals;
using System.Collections.ObjectModel;

namespace SprintForge.ViewModels;

public sealed partial class ApprovalsViewModel : ObservableObject
{
    private readonly IApprovalGate _approvalGate;
    private ObservableCollection<ApprovalRequest> _pendingBacking = [];
    private ObservableCollection<ApprovalRequest> _historyBacking = [];

    [ObservableProperty] private ObservableCollection<ApprovalRequest> _currentItems = [];
    [ObservableProperty] private ApprovalRequest? _selectedItem;
    [ObservableProperty] private string _activeTab    = "Pending";
    [ObservableProperty] private int    _pendingCount;
    [ObservableProperty] private string _statusMessage = string.Empty;

    public bool IsPendingTabActive => ActiveTab == "Pending";
    public bool IsHistoryTabActive => ActiveTab == "History";

    public ApprovalsViewModel(IApprovalGate approvalGate)
    {
        _approvalGate = approvalGate;
        _ = LoadAsync();
    }

    partial void OnActiveTabChanged(string value)
    {
        OnPropertyChanged(nameof(IsPendingTabActive));
        OnPropertyChanged(nameof(IsHistoryTabActive));
    }

    public async Task LoadAsync()
    {
        try
        {
            var pending = await _approvalGate.GetPendingAsync();
            _pendingBacking = new ObservableCollection<ApprovalRequest>(pending);
            PendingCount    = _pendingBacking.Count;

            var history = await _approvalGate.GetHistoryAsync(50);
            _historyBacking = new ObservableCollection<ApprovalRequest>(history);

            CurrentItems = ActiveTab == "Pending" ? _pendingBacking : _historyBacking;
            SelectedItem = CurrentItems.FirstOrDefault();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading approvals: {ex.Message}";
        }
    }

    [RelayCommand]
    private void ShowPendingTab()
    {
        ActiveTab    = "Pending";
        CurrentItems = _pendingBacking;
        SelectedItem = CurrentItems.FirstOrDefault();
    }

    [RelayCommand]
    private void ShowHistoryTab()
    {
        ActiveTab    = "History";
        CurrentItems = _historyBacking;
        SelectedItem = CurrentItems.FirstOrDefault();
    }

    [RelayCommand]
    private async Task ApproveAsync(ApprovalRequest? request)
    {
        if (request is null) return;
        await RecordDecisionAsync(request, ApprovalStatus.Approved);
    }

    [RelayCommand]
    private async Task RejectAsync(ApprovalRequest? request)
    {
        if (request is null) return;
        await RecordDecisionAsync(request, ApprovalStatus.Rejected);
    }

    [RelayCommand]
    private async Task ApproveAllAsync()
    {
        var items = _pendingBacking.ToList();
        foreach (var item in items) await RecordDecisionAsync(item, ApprovalStatus.Approved);
        StatusMessage = $"Approved {items.Count} item(s).";
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadAsync();
        StatusMessage = $"Refreshed at {DateTime.Now:HH:mm:ss}";
    }

    private async Task RecordDecisionAsync(ApprovalRequest request, ApprovalStatus status)
    {
        try
        {
            var decision = new ApprovalDecision
            {
                ApprovalId      = request.ApprovalId,
                Decision        = status,
                DecidedAt       = DateTimeOffset.UtcNow,
                DecidedByUser   = Environment.UserName
            };
            await _approvalGate.ApplyDecisionAsync(decision);
            await LoadAsync();
            StatusMessage = $"{status}: {request.OperationDescription[..Math.Min(60, request.OperationDescription.Length)]}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
    }
}
