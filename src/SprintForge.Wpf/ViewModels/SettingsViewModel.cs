using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SprintForge.Application.Configuration;
using System.Collections.ObjectModel;

namespace SprintForge.Wpf.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly IProfileStore _profileStore;

    // ── Tab navigation ──────────────────────────────────────────────────────
    [ObservableProperty] private string _activeTab = "General";

    // ── General ─────────────────────────────────────────────────────────────
    [ObservableProperty] private string _profileName      = "Default";
    [ObservableProperty] private string _workingDirectory = @"C:\Users\akshay.patwari\AppData\Roaming\SprintForge";
    [ObservableProperty] private string _userDisplayName  = "Akshay Patwari";
    [ObservableProperty] private string _userEmail        = "akshay.patwari@sdc.com";
    [ObservableProperty] private int    _retentionDays    = 365;

    // ── Jira ────────────────────────────────────────────────────────────────
    [ObservableProperty] private string _jiraUrl               = "https://jira.sdc.com";
    [ObservableProperty] private string _jiraUsername          = "akshay.patwari@sdc.com";
    [ObservableProperty] private string _jiraApiToken          = "";
    [ObservableProperty] private string _jiraProjectKeys       = "RBP, FNT, NOT, AUM";
    [ObservableProperty] private string _jiraDefaultIssueType  = "Story";
    [ObservableProperty] private string _jiraDefaultPriority   = "Medium";
    [ObservableProperty] private string _jiraDefaultLabels     = "";
    [ObservableProperty] private string _jiraDefaultComponents = "";
    [ObservableProperty] private string _jiraStoryPointsField  = "customfield_10016";
    [ObservableProperty] private string _jiraSprintField       = "customfield_10020";
    [ObservableProperty] private string _jiraEpicLinkField     = "customfield_10014";
    [ObservableProperty] private string _connectionStatus      = "Not tested";
    [ObservableProperty] private bool   _isConnected;

    public IReadOnlyList<string> JiraIssueTypes { get; } = ["Story", "Task", "Sub-task", "Bug", "Epic"];
    public IReadOnlyList<string> JiraPriorities  { get; } = ["Highest", "High", "Medium", "Low", "Lowest"];

    // ── AI Provider (opencode format) ───────────────────────────────────────
    // Model is specified as  provider/model-id  matching the opencode config schema.
    // base_url is optional — leave blank for cloud providers; set for Ollama/custom.
    [ObservableProperty] private string _aiModel       = "anthropic/claude-sonnet-5";
    [ObservableProperty] private string _aiBaseUrl     = "";
    [ObservableProperty] private string _aiApiKey      = "";
    [ObservableProperty] private double _aiTemperature = 0.7;
    [ObservableProperty] private int    _aiMaxTokens   = 8192;

    // Grouped by provider in  provider/model  format — mirrors opencode's model list.
    public IReadOnlyList<string> AnthropicModels { get; } =
    [
        "anthropic/claude-sonnet-5",
        "anthropic/claude-opus-4-8",
        "anthropic/claude-haiku-4-5-20251001",
        "anthropic/claude-fable-5",
    ];
    public IReadOnlyList<string> OpenAiModels { get; } =
    [
        "openai/gpt-4o",
        "openai/gpt-4o-mini",
        "openai/o3",
        "openai/o4-mini",
    ];
    public IReadOnlyList<string> GoogleModels { get; } =
    [
        "google/gemini-2.5-pro",
        "google/gemini-2.5-flash",
    ];
    public IReadOnlyList<string> OllamaModels { get; } =
    [
        "ollama/llama3.3",
        "ollama/qwen2.5-coder",
        "ollama/deepseek-r1",
    ];

    // ── Repositories ────────────────────────────────────────────────────────
    public ObservableCollection<RepositoryEntry> Repositories { get; } = [];
    [ObservableProperty] private string _newRepoName      = "";
    [ObservableProperty] private string _newRepoUrl       = "";
    [ObservableProperty] private string _newRepoBranch    = "main";
    [ObservableProperty] private string _newRepoTechStack = "Java";
    [ObservableProperty] private string _newRepoType      = "GitHub";

    public IReadOnlyList<string> TechStacks { get; } =
        ["Java", "Python", "TypeScript / React", "TypeScript / Angular", "Vue.js", "Go", "C# / .NET", "Other"];
    public IReadOnlyList<string> RepoTypes { get; } = ["GitHub", "GitLab", "Bitbucket", "Azure DevOps"];

    // ── SRS Config ──────────────────────────────────────────────────────────
    [ObservableProperty] private string _srsIdFormat            = "SRS-{PROJECT}-{NUMBER:D4}";
    [ObservableProperty] private string _srsDefaultProject      = "";
    [ObservableProperty] private string _srsDescriptionTemplate =
        "## Overview\n{description}\n\n## Services Involved\n{services}\n\n## Functional Requirements\n{requirements}\n\n## Limitations & Out of Scope\n{limitations}";
    [ObservableProperty] private string _srsInvolvedServices    = "";
    [ObservableProperty] private string _srsLimitationsTemplate =
        "The following are explicitly out of scope for this deliverable:\n- {item}";

    // ── Sprint ──────────────────────────────────────────────────────────────
    [ObservableProperty] private int    _sprintDurationDays       = 14;
    [ObservableProperty] private int    _sprintDevelopmentDays    = 10;
    [ObservableProperty] private int    _sprintBufferDays         = 2;
    [ObservableProperty] private int    _sprintWorkingHoursPerDay = 8;
    [ObservableProperty] private string _sprintPlanSource         = "Text";
    [ObservableProperty] private string _sprintPlanText           = "";
    [ObservableProperty] private string _confluenceUrl            = "";
    [ObservableProperty] private string _confluenceUsername       = "";
    [ObservableProperty] private string _confluenceSpaceKey       = "";
    [ObservableProperty] private bool   _autoCreateSubtasks       = true;
    [ObservableProperty] private string _defaultSubtaskTypes      = "Development, Code Review, Unit Tests, Documentation";

    public IReadOnlyList<string> SprintPlanSources { get; } = ["Text", "Confluence"];

    // ── Testing ─────────────────────────────────────────────────────────────
    [ObservableProperty] private int  _globalCoverageTarget    = 80;
    [ObservableProperty] private int  _javaCoverageTarget      = 80;
    [ObservableProperty] private int  _pythonCoverageTarget    = 80;
    [ObservableProperty] private int  _uiCoverageTarget        = 70;
    [ObservableProperty] private bool _failBuildOnCoverageMiss = true;

    // ── Preferences ─────────────────────────────────────────────────────────
    [ObservableProperty] private string _minimumLogLevel = "Information";
    [ObservableProperty] private bool   _autoSave        = true;

    public IReadOnlyList<string> LogLevels { get; } =
        ["Verbose", "Debug", "Information", "Warning", "Error", "Fatal"];

    public SettingsViewModel(IProfileStore profileStore)
    {
        _profileStore = profileStore;

        Repositories.Add(new RepositoryEntry
        {
            Name = "retail-banking-platform", Url = "https://github.com/sdc/retail-banking-platform",
            Branch = "main", TechStack = "Java", Type = "GitHub"
        });
        Repositories.Add(new RepositoryEntry
        {
            Name = "banking-ui", Url = "https://github.com/sdc/banking-ui",
            Branch = "main", TechStack = "TypeScript / React", Type = "GitHub"
        });
    }

    [RelayCommand] private void SetTab(string tab) => ActiveTab = tab;

    [RelayCommand] private void SetModel(string model) => AiModel = model;

    [RelayCommand]
    private async Task TestJiraConnection()
    {
        ConnectionStatus = "Testing...";
        IsConnected = false;
        await Task.Delay(2000);
        ConnectionStatus = "✓ Connected";
        IsConnected = true;
    }

    [RelayCommand] private void BrowseWorkingDirectory() { }

    [RelayCommand]
    private void AddRepository()
    {
        if (string.IsNullOrWhiteSpace(NewRepoName) || string.IsNullOrWhiteSpace(NewRepoUrl))
            return;
        Repositories.Add(new RepositoryEntry
        {
            Name      = NewRepoName,
            Url       = NewRepoUrl,
            Branch    = string.IsNullOrWhiteSpace(NewRepoBranch) ? "main" : NewRepoBranch,
            TechStack = NewRepoTechStack,
            Type      = NewRepoType
        });
        NewRepoName   = "";
        NewRepoUrl    = "";
        NewRepoBranch = "main";
    }

    [RelayCommand] private void RemoveRepository(RepositoryEntry entry) => Repositories.Remove(entry);

    [RelayCommand] private async Task SaveChanges() => await Task.Delay(500);
}
