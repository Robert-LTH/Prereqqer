using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text.Json;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Prereqqer.App.Resources;
using Prereqqer.Core.Definitions;
using Prereqqer.Core.Execution;
using Prereqqer.Core.Storage;
using Prereqqer.Core.Validation;

namespace Prereqqer.App.ViewModels;

public sealed partial class MainViewModel : ViewModelBase
{
    private readonly ConditionDefinitionStore _store;
    private readonly IConditionExecutor _conditionExecutor;
    private readonly DefinitionValidator _validator = new();
    private readonly string _startupConfigurationUrl;
    private BrandingDefinition _branding = new();
    private ConfigurationSourceDefinition _configurationSource = new();
    private CancellationTokenSource? _busyCancellationTokenSource;
    private readonly Dictionary<ConditionEditorViewModel, CancellationTokenSource> _conditionRunTokens = new();
    private int _activeRunCount;

    [ObservableProperty]
    private ObservableCollection<GroupEditorViewModel> _groups = [];

    [ObservableProperty]
    private GroupEditorViewModel? _selectedGroup;

    [ObservableProperty]
    private CheckOutcome? _overallOutcome;

    [ObservableProperty]
    private string _overallMessage = Strings.DefinitionsHaveNotBeenRun;

    [ObservableProperty]
    private string _statusMessage = Strings.LoadingDefinitions;

    [ObservableProperty]
    private string _definitionPath;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _isDesignMode;

    [ObservableProperty]
    private bool _hasUnsavedChanges;

    public MainViewModel()
        : this(isDesignMode: false)
    {
    }

    public MainViewModel(bool isDesignMode)
        : this(isDesignMode, string.Empty)
    {
    }

    public MainViewModel(bool isDesignMode, string configurationUrl)
        : this(new ConditionDefinitionStore(), new ConditionExecutor(), isDesignMode, configurationUrl)
    {
    }

    public MainViewModel(
        ConditionDefinitionStore store,
        IConditionExecutor conditionExecutor,
        bool isDesignMode = false,
        string configurationUrl = "")
    {
        _store = store;
        _conditionExecutor = conditionExecutor;
        _startupConfigurationUrl = configurationUrl ?? string.Empty;
        _definitionPath = store.Path;
        _isDesignMode = isDesignMode;

        _ = LoadDefinitionsAsync();
    }

    public string WindowTitle => IsDesignMode ? Format(Strings.DesignWindowTitleFormat, BrandTitle) : BrandTitle;

    public string BrandName
    {
        get => _branding.ApplicationName;
        set
        {
            if (SetProperty(_branding.ApplicationName, value ?? string.Empty, _branding, static (model, newValue) => model.ApplicationName = newValue))
            {
                OnPropertyChanged(nameof(WindowTitle));
                MarkConfigurationChanged();
            }
        }
    }

    public string BrandSubtitle
    {
        get => _branding.Subtitle;
        set
        {
            if (SetProperty(_branding.Subtitle, value ?? string.Empty, _branding, static (model, newValue) => model.Subtitle = newValue))
            {
                MarkConfigurationChanged();
            }
        }
    }

    public string HeaderBackgroundColor
    {
        get => _branding.HeaderBackgroundColor;
        set
        {
            if (SetProperty(_branding.HeaderBackgroundColor, value ?? string.Empty, _branding, static (model, newValue) => model.HeaderBackgroundColor = newValue))
            {
                OnPropertyChanged(nameof(HeaderBackgroundBrush));
                MarkConfigurationChanged();
            }
        }
    }

    public string HeaderForegroundColor
    {
        get => _branding.HeaderForegroundColor;
        set
        {
            if (SetProperty(_branding.HeaderForegroundColor, value ?? string.Empty, _branding, static (model, newValue) => model.HeaderForegroundColor = newValue))
            {
                OnPropertyChanged(nameof(HeaderForegroundBrush));
                MarkConfigurationChanged();
            }
        }
    }

    public string AccentColor
    {
        get => _branding.AccentColor;
        set
        {
            if (SetProperty(_branding.AccentColor, value ?? string.Empty, _branding, static (model, newValue) => model.AccentColor = newValue))
            {
                OnPropertyChanged(nameof(AccentBrush));
                MarkConfigurationChanged();
            }
        }
    }

    public string ConfigurationUrl
    {
        get => _configurationSource.Url;
        set
        {
            if (SetProperty(_configurationSource.Url, value ?? string.Empty, _configurationSource, static (model, newValue) => model.Url = newValue))
            {
                OnPropertyChanged(nameof(HasConfigurationUrl));
                MarkConfigurationChanged();
            }
        }
    }

    public bool FetchConfigurationOnStartup
    {
        get => _configurationSource.FetchOnStartup;
        set
        {
            if (SetProperty(_configurationSource.FetchOnStartup, value, _configurationSource, static (model, newValue) => model.FetchOnStartup = newValue))
            {
                MarkConfigurationChanged();
            }
        }
    }

    public int ConfigurationTimeoutSeconds
    {
        get => _configurationSource.TimeoutSeconds;
        set
        {
            if (SetProperty(
                _configurationSource.TimeoutSeconds,
                Math.Clamp(value, 1, 300),
                _configurationSource,
                static (model, newValue) => model.TimeoutSeconds = newValue))
            {
                MarkConfigurationChanged();
            }
        }
    }

    public IBrush HeaderBackgroundBrush => ParseBrush(HeaderBackgroundColor, "#172033");

    public IBrush HeaderForegroundBrush => ParseBrush(HeaderForegroundColor, "#FFFFFF");

    public IBrush AccentBrush => ParseBrush(AccentColor, "#075EAD");

    public string OverallOutcomeDisplay => OutcomePresentation.ToDisplayName(OverallOutcome);

    public IBrush OverallOutcomeBackgroundBrush => OutcomePresentation.ToBackgroundBrush(OverallOutcome);

    public IBrush OverallOutcomeBorderBrush => OutcomePresentation.ToBorderBrush(OverallOutcome);

    public IBrush OverallOutcomeForegroundBrush => OutcomePresentation.ToForegroundBrush(OverallOutcome);

    public bool IsReady => OverallOutcome is not CheckOutcome.Error and not CheckOutcome.Cancelled;

    public bool HasSelectedGroup => SelectedGroup is not null;

    public bool HasSelectedCondition => SelectedGroup?.SelectedCondition is not null;

    public bool HasSelectedRule => SelectedGroup?.SelectedCondition?.SelectedRule is not null;

    public bool HasConfigurationUrl => !string.IsNullOrWhiteSpace(ConfigurationUrl);

    public bool HasRunResults => Groups.SelectMany(group => group.Conditions).Any(condition => condition.LastOutcome.HasValue);

    public ConditionEditorViewModel? SelectedCondition => SelectedGroup?.SelectedCondition;

    partial void OnOverallOutcomeChanged(CheckOutcome? value)
    {
        OnPropertyChanged(nameof(OverallOutcomeDisplay));
        OnPropertyChanged(nameof(OverallOutcomeBackgroundBrush));
        OnPropertyChanged(nameof(OverallOutcomeBorderBrush));
        OnPropertyChanged(nameof(OverallOutcomeForegroundBrush));
        OnPropertyChanged(nameof(IsReady));
    }

    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(CanCancelRun));
    }

    partial void OnIsDesignModeChanged(bool value)
    {
        OnPropertyChanged(nameof(WindowTitle));
    }

    public void EnableDesignMode(string message)
    {
        IsDesignMode = true;
        StatusMessage = message;
    }

    partial void OnSelectedGroupChanged(GroupEditorViewModel? value)
    {
        if (value is not null && value.SelectedCondition is null && value.Conditions.Count > 0)
        {
            value.SelectedCondition = value.Conditions[0];
        }

        OnSelectionChanged();
    }

    [RelayCommand]
    private async Task LoadDefinitionsAsync()
    {
        await RunBusyAsync(async cancellationToken =>
        {
            var localDocument = await _store.LoadOrCreateAsync(cancellationToken).ConfigureAwait(true);
            var source = localDocument.ConfigurationSource;
            var startupUrl = _startupConfigurationUrl.Trim();
            var configurationUrl = string.IsNullOrWhiteSpace(startupUrl) ? source.Url : startupUrl;
            var shouldFetchRemote = !string.IsNullOrWhiteSpace(startupUrl)
                || (source.FetchOnStartup && !string.IsNullOrWhiteSpace(source.Url));

            if (shouldFetchRemote)
            {
                try
                {
                    var remoteDocument = await _store.LoadFromUrlAsync(
                        configurationUrl,
                        source.TimeoutSeconds,
                        cancellationToken).ConfigureAwait(true);

                    remoteDocument.ConfigurationSource = BuildEffectiveConfigurationSource(source, configurationUrl);
                    LoadDocument(remoteDocument);
                    await _store.SaveAsync(BuildDocument(), cancellationToken).ConfigureAwait(true);
                    StatusMessage = Format(Strings.LoadedDefinitionsFromUrlFormat, configurationUrl, DefinitionPath);
                    return;
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    LoadDocument(localDocument);
                    StatusMessage = Format(Strings.LoadedLocalDefinitionsConfigurationUrlFailedFormat, DefinitionPath, exception.Message);
                    return;
                }
            }

            LoadDocument(localDocument);
            StatusMessage = Format(Strings.LoadedDefinitionsFormat, DefinitionPath);
        }).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task SaveDefinitionsAsync()
    {
        await RunBusyAsync(async cancellationToken =>
        {
            await _store.SaveAsync(BuildDocument(), cancellationToken).ConfigureAwait(true);
            HasUnsavedChanges = false;
            StatusMessage = Format(Strings.SavedDefinitionsFormat, DefinitionPath);
        }).ConfigureAwait(true);
    }

    public async Task ImportDefinitionsAsync(string path)
    {
        await RunBusyAsync(async cancellationToken =>
        {
            var document = await _store.LoadAsync(path, cancellationToken).ConfigureAwait(true);
            LoadDocument(document);
            await _store.SaveAsync(BuildDocument(), cancellationToken).ConfigureAwait(true);
            HasUnsavedChanges = false;
            StatusMessage = Format(Strings.ImportedDefinitionsFormat, path);
        }).ConfigureAwait(true);
    }

    public async Task ExportDefinitionsAsync(string path)
    {
        await RunBusyAsync(async cancellationToken =>
        {
            await _store.SaveAsync(BuildDocument(), path, cancellationToken).ConfigureAwait(true);
            StatusMessage = Format(Strings.ExportedDefinitionsFormat, path);
        }).ConfigureAwait(true);
    }

    public async Task SaveResultsAsync(string path)
    {
        if (!HasRunResults)
        {
            StatusMessage = Strings.RunOneOrMoreConditionsBeforeSavingResults;
            return;
        }

        await RunBusyAsync(async cancellationToken =>
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await using var stream = File.Create(path);
            await JsonSerializer.SerializeAsync(
                stream,
                BuildSavedResultDocument(),
                ConditionDefinitionStore.JsonOptions,
                cancellationToken).ConfigureAwait(true);
            StatusMessage = Format(Strings.SavedResultsFormat, path);
        }).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task FetchConfigurationFromUrlAsync()
    {
        if (string.IsNullOrWhiteSpace(ConfigurationUrl))
        {
            StatusMessage = Strings.EnterConfigurationUrlBeforeFetching;
            return;
        }

        await RunBusyAsync(async cancellationToken =>
        {
            var source = BuildConfigurationSource();
            var document = await _store.LoadFromUrlAsync(
                source.Url,
                source.TimeoutSeconds,
                cancellationToken).ConfigureAwait(true);

            document.ConfigurationSource = source;
            LoadDocument(document);
            await _store.SaveAsync(BuildDocument(), cancellationToken).ConfigureAwait(true);
            HasUnsavedChanges = false;
            StatusMessage = Format(Strings.FetchedDefinitionsFormat, source.Url, DefinitionPath);
        }).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task RunAllAsync()
    {
        await RunBusyAsync(async cancellationToken =>
        {
            var stopwatch = Stopwatch.StartNew();

            foreach (var group in Groups)
            {
                group.ClearResult();
            }

            OverallOutcome = null;
            OverallMessage = Strings.RunningAllGroups;
            ReevaluateStatuses();

            foreach (var group in Groups)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await RunGroupForUiAsync(group, cancellationToken).ConfigureAwait(true);
                ReevaluateStatuses();
            }

            stopwatch.Stop();
            ReevaluateStatuses();
            StatusMessage = Format(Strings.CompletedAllGroupsFormat, FormatDuration(stopwatch.Elapsed));
            NotifyResultsChanged();
        }).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task RunGroupAsync(GroupEditorViewModel? group)
    {
        if (group is null)
        {
            return;
        }

        await RunBusyAsync(async cancellationToken =>
        {
            group.ClearResult();
            OverallMessage = Format(Strings.RunningItemFormat, group.Name);
            ReevaluateStatuses();

            var duration = await RunGroupForUiAsync(group, cancellationToken).ConfigureAwait(true);
            ReevaluateStatuses();
            StatusMessage = Format(Strings.CompletedGroupFormat, group.Name, FormatDuration(duration));
            NotifyResultsChanged();
        }).ConfigureAwait(true);
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task RunConditionAsync(ConditionEditorViewModel? condition)
    {
        if (condition is null)
        {
            return;
        }

        var group = Groups.FirstOrDefault(item => item.Conditions.Contains(condition));
        if (group is null)
        {
            return;
        }

        if (condition.IsRunning)
        {
            return;
        }

        if (group.IsRunning)
        {
            StatusMessage = Strings.WaitForRunningGroupBeforeAnotherCheck;
            return;
        }

        if (HasRunningConditionOutsideGroup(group))
        {
            StatusMessage = Strings.WaitForRunningGroupBeforeAnotherGroup;
            return;
        }

        BeginRun();

        try
        {
            group.IsExpanded = true;
            OverallMessage = Format(Strings.RunningItemFormat, condition.Name);
            ConditionExecutionResult result;
            if (!group.Enabled)
            {
                condition.ClearResult();
                result = new ConditionExecutionResult
                {
                    ConditionId = condition.Model.Id,
                    Outcome = CheckOutcome.PassedWithWarning,
                    Message = Strings.GroupDisabledConditionNotRun,
                    Duration = TimeSpan.Zero,
                    RecommendedAction = condition.RecommendedAction
                };
                condition.ApplyResult(result);
            }
            else
            {
                result = await RunRegisteredConditionForUiAsync(condition, CancellationToken.None).ConfigureAwait(true);
            }

            group.LastMessage = Strings.UpdatedFromIndividualConditionRun;
            ReevaluateStatuses();
            StatusMessage = result.Message == Strings.CancelledMessage
                ? Format(Strings.CancelledConditionFormat, condition.Name)
                : Format(Strings.CompletedConditionFormat, condition.Name, condition.LastOutcomeDisplay);
            NotifyResultsChanged();
        }
        catch (OperationCanceledException)
        {
            StatusMessage = Format(Strings.CancelledConditionFormat, condition.Name);
        }
        finally
        {
            EndRun();
            ReevaluateStatuses();
        }
    }

    [RelayCommand]
    private void CancelRun()
    {
        _busyCancellationTokenSource?.Cancel();
        foreach (var tokenSource in _conditionRunTokens.Values)
        {
            tokenSource.Cancel();
        }
    }

    [RelayCommand]
    private void CancelConditionRun(ConditionEditorViewModel? condition)
    {
        if (condition is not null && _conditionRunTokens.TryGetValue(condition, out var tokenSource))
        {
            tokenSource.Cancel();
        }
    }

    public bool CanCancelRun => IsBusy;

    [RelayCommand]
    private static void ToggleGroupExpansion(GroupEditorViewModel? group)
    {
        if (group is not null)
        {
            group.IsExpanded = !group.IsExpanded;
        }
    }

    [RelayCommand]
    private static void ToggleConditionExpansion(ConditionEditorViewModel? condition)
    {
        if (condition is not null)
        {
            condition.IsExpanded = !condition.IsExpanded;
        }
    }

    [RelayCommand]
    private void AddGroup()
    {
        var group = new GroupEditorViewModel(new ConditionGroupDefinition
        {
            Name = Format(Strings.GroupNameFormat, Groups.Count + 1)
        }, MarkConfigurationChanged);

        Groups.Add(group);
        SelectedGroup = group;
        MarkConfigurationChanged();
        StatusMessage = Strings.AddedGroup;
    }

    [RelayCommand]
    private void DeleteGroup(GroupEditorViewModel? group = null)
    {
        group ??= SelectedGroup;
        if (group is null)
        {
            return;
        }

        var index = Groups.IndexOf(group);
        if (index < 0)
        {
            return;
        }

        Groups.Remove(group);
        SelectedGroup = Groups.Count == 0 ? null : Groups[Math.Clamp(index, 0, Groups.Count - 1)];
        MarkConfigurationChanged();
        StatusMessage = Strings.DeletedGroup;
    }

    [RelayCommand]
    private void MoveGroupUp(GroupEditorViewModel? group = null)
    {
        if (MoveSelected(Groups, group ?? SelectedGroup, -1, value => SelectedGroup = value))
        {
            MarkConfigurationChanged();
        }
    }

    [RelayCommand]
    private void MoveGroupDown(GroupEditorViewModel? group = null)
    {
        if (MoveSelected(Groups, group ?? SelectedGroup, 1, value => SelectedGroup = value))
        {
            MarkConfigurationChanged();
        }
    }

    [RelayCommand]
    private void AddCondition()
    {
        if (SelectedGroup is null)
        {
            return;
        }

        var condition = new ConditionEditorViewModel(DefinitionFactory.CreateDefaultPowerShellCondition(), MarkConfigurationChanged)
        {
            Name = Format(Strings.ConditionNameFormat, SelectedGroup.Conditions.Count + 1)
        };

        SelectedGroup.Conditions.Add(condition);
        SelectedGroup.SelectedCondition = condition;
        MarkConfigurationChanged();
        OnSelectionChanged();
        StatusMessage = Strings.AddedCondition;
    }

    [RelayCommand]
    private void CopyFactoryChecks()
    {
        var factoryDocument = DefinitionFactory.CreateDefaultDocument();
        var addedGroupCount = 0;
        var addedConditionCount = 0;
        GroupEditorViewModel? firstChangedGroup = null;
        ConditionEditorViewModel? firstAddedCondition = null;

        foreach (var factoryGroup in factoryDocument.Groups)
        {
            var targetGroup = Groups.FirstOrDefault(group =>
                string.Equals(group.Name, factoryGroup.Name, StringComparison.OrdinalIgnoreCase));

            if (targetGroup is null)
            {
                targetGroup = new GroupEditorViewModel(CloneDefinition(factoryGroup), MarkConfigurationChanged);
                Groups.Add(targetGroup);
                addedGroupCount++;
                addedConditionCount += targetGroup.Conditions.Count;
                firstChangedGroup ??= targetGroup;
                firstAddedCondition ??= targetGroup.Conditions.FirstOrDefault();
                continue;
            }

            foreach (var factoryCondition in factoryGroup.Conditions)
            {
                var conditionExists = targetGroup.Conditions.Any(condition =>
                    string.Equals(condition.Model.Id, factoryCondition.Id, StringComparison.Ordinal)
                    || string.Equals(condition.Name, factoryCondition.Name, StringComparison.OrdinalIgnoreCase));

                if (conditionExists)
                {
                    continue;
                }

                var condition = new ConditionEditorViewModel(CloneDefinition(factoryCondition), MarkConfigurationChanged);
                targetGroup.Conditions.Add(condition);
                addedConditionCount++;
                firstChangedGroup ??= targetGroup;
                firstAddedCondition ??= condition;
            }
        }

        if (addedGroupCount == 0 && addedConditionCount == 0)
        {
            StatusMessage = Strings.NoFactoryChecksCopied;
            return;
        }

        if (firstChangedGroup is not null)
        {
            SelectedGroup = firstChangedGroup;
            firstChangedGroup.SelectedCondition = firstAddedCondition ?? firstChangedGroup.SelectedCondition;
        }

        MarkConfigurationChanged();
        OnSelectionChanged();
        StatusMessage = Format(Strings.CopiedFactoryChecksFormat, addedConditionCount, addedGroupCount);
    }

    [RelayCommand]
    private void DeleteCondition(ConditionEditorViewModel? condition = null)
    {
        condition ??= SelectedGroup?.SelectedCondition;
        if (SelectedGroup is null || condition is null)
        {
            return;
        }

        var index = SelectedGroup.Conditions.IndexOf(condition);
        if (index < 0)
        {
            return;
        }

        SelectedGroup.Conditions.Remove(condition);
        SelectedGroup.SelectedCondition = SelectedGroup.Conditions.Count == 0
            ? null
            : SelectedGroup.Conditions[Math.Clamp(index, 0, SelectedGroup.Conditions.Count - 1)];
        MarkConfigurationChanged();
        OnSelectionChanged();
        StatusMessage = Strings.DeletedCondition;
    }

    [RelayCommand]
    private void MoveConditionUp(ConditionEditorViewModel? condition = null)
    {
        if (SelectedGroup is null)
        {
            return;
        }

        if (MoveSelected(SelectedGroup.Conditions, condition ?? SelectedGroup.SelectedCondition, -1, value => SelectedGroup.SelectedCondition = value))
        {
            MarkConfigurationChanged();
        }
    }

    [RelayCommand]
    private void MoveConditionDown(ConditionEditorViewModel? condition = null)
    {
        if (SelectedGroup is null)
        {
            return;
        }

        if (MoveSelected(SelectedGroup.Conditions, condition ?? SelectedGroup.SelectedCondition, 1, value => SelectedGroup.SelectedCondition = value))
        {
            MarkConfigurationChanged();
        }
    }

    [RelayCommand]
    private void AddRule()
    {
        var condition = SelectedGroup?.SelectedCondition;
        if (condition is null)
        {
            return;
        }

        var rule = new RuleEditorViewModel(new OutcomeRuleDefinition
        {
            Name = Format(Strings.RuleNameFormat, condition.Rules.Count + 1),
            Target = RuleTarget.RowCount,
            Operator = RuleOperator.GreaterThan,
            ExpectedValue = "0"
        }, condition.MarkDefinitionChanged);

        condition.AddRule(rule);
        condition.SelectedRule = rule;
        OnSelectionChanged();
        StatusMessage = Strings.AddedRule;
    }

    [RelayCommand]
    private void DeleteRule(RuleEditorViewModel? rule = null)
    {
        var condition = SelectedGroup?.SelectedCondition;
        rule ??= condition?.SelectedRule;
        if (condition is null || rule is null)
        {
            return;
        }

        var index = condition.Rules.IndexOf(rule);
        if (index < 0)
        {
            return;
        }

        condition.RemoveRule(rule);
        condition.SelectedRule = condition.Rules.Count == 0
            ? null
            : condition.Rules[Math.Clamp(index, 0, condition.Rules.Count - 1)];
        OnSelectionChanged();
        StatusMessage = Strings.DeletedRule;
    }

    [RelayCommand]
    private void MoveRuleUp(RuleEditorViewModel? rule = null)
    {
        var condition = SelectedGroup?.SelectedCondition;
        if (condition is null)
        {
            return;
        }

        MoveSelectedRule(condition, rule ?? condition.SelectedRule, -1);
    }

    [RelayCommand]
    private void MoveRuleDown(RuleEditorViewModel? rule = null)
    {
        var condition = SelectedGroup?.SelectedCondition;
        if (condition is null)
        {
            return;
        }

        MoveSelectedRule(condition, rule ?? condition.SelectedRule, 1);
    }

    [RelayCommand]
    private void ValidateSelectedCondition()
    {
        var condition = SelectedGroup?.SelectedCondition;
        if (condition is null)
        {
            return;
        }

        var validation = _validator.ValidateCondition(condition.ToModel());
        condition.ValidationSummary = FormatValidation(validation);
        StatusMessage = validation.IsValid ? Strings.ConditionIsValid : Strings.ConditionHasValidationErrors;
    }

    [RelayCommand]
    private async Task TestSelectedConditionAsync()
    {
        var condition = SelectedGroup?.SelectedCondition;
        if (condition is null)
        {
            return;
        }

        var validation = _validator.ValidateCondition(condition.ToModel());
        condition.ValidationSummary = FormatValidation(validation);
        if (!validation.IsValid)
        {
            StatusMessage = Strings.ConditionTestSkippedValidationFailed;
            return;
        }

        await RunBusyAsync(async cancellationToken =>
        {
            var result = await RunRegisteredConditionForUiAsync(condition, cancellationToken).ConfigureAwait(true);
            StatusMessage = Format(Strings.TestCompletedFormat, OutcomePresentation.ToDisplayName(result.Outcome));
            NotifyResultsChanged();
        }).ConfigureAwait(true);
    }

    private void LoadDocument(ConditionDefinitionDocument document)
    {
        var previousConditions = Groups
            .SelectMany(group => group.Conditions)
            .GroupBy(condition => condition.Model.Id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        _branding = document.Branding;
        _configurationSource = document.ConfigurationSource;
        NotifyBrandingChanged();
        NotifyConfigurationSourceChanged();

        Groups = new ObservableCollection<GroupEditorViewModel>(
            document.Groups.Select(group => new GroupEditorViewModel(group, MarkConfigurationChanged)));

        foreach (var condition in Groups.SelectMany(group => group.Conditions))
        {
            if (previousConditions.TryGetValue(condition.Model.Id, out var previous)
                && previous.Version == condition.Version)
            {
                condition.CopyRuntimeStateFrom(previous);
            }
        }

        SelectedGroup = Groups.FirstOrDefault();
        OverallOutcome = null;
        ReevaluateStatuses();
        NotifyResultsChanged();
        HasUnsavedChanges = false;
    }

    private ConditionDefinitionDocument BuildDocument() =>
        new()
        {
            SchemaVersion = 1,
            Branding = _branding,
            ConfigurationSource = BuildConfigurationSource(),
            Groups = Groups.Select(group => group.ToModel()).ToList()
        };

    private ConfigurationSourceDefinition BuildConfigurationSource() =>
        new()
        {
            Url = ConfigurationUrl.Trim(),
            FetchOnStartup = FetchConfigurationOnStartup,
            TimeoutSeconds = Math.Clamp(ConfigurationTimeoutSeconds, 1, 300)
        };

    private static ConfigurationSourceDefinition BuildEffectiveConfigurationSource(
        ConfigurationSourceDefinition source,
        string configurationUrl) =>
        new()
        {
            Url = configurationUrl.Trim(),
            FetchOnStartup = source.FetchOnStartup,
            TimeoutSeconds = Math.Clamp(source.TimeoutSeconds, 1, 300)
        };

    private static T CloneDefinition<T>(T value)
    {
        var json = JsonSerializer.Serialize(value, ConditionDefinitionStore.JsonOptions);
        return JsonSerializer.Deserialize<T>(json, ConditionDefinitionStore.JsonOptions)
            ?? throw new InvalidOperationException("Definition could not be copied.");
    }

    private async Task<TimeSpan> RunGroupForUiAsync(
        GroupEditorViewModel group,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        group.IsRunning = true;
        group.LastOutcome = null;
        group.LastMessage = Strings.Running;

        try
        {
            if (!group.Enabled)
            {
                group.LastOutcome = CheckOutcome.PassedWithWarning;
                group.LastMessage = Strings.GroupDisabledNoConditionsRun;
                ReevaluateStatuses();
                return stopwatch.Elapsed;
            }

            await RunConditionsInGroupOrderAsync(group, cancellationToken).ConfigureAwait(true);

            ReevaluateStatuses();
            group.LastOutcome = Aggregate(group.Conditions.Select(condition => condition.LastOutcome)) ?? CheckOutcome.Passed;
            group.LastMessage = Format(Strings.CompletedInFormat, FormatDuration(stopwatch.Elapsed));
            ReevaluateStatuses();
            return stopwatch.Elapsed;
        }
        finally
        {
            stopwatch.Stop();
            group.IsRunning = false;
        }
    }

    private async Task<ConditionExecutionResult> RunConditionForUiAsync(
        ConditionEditorViewModel condition,
        CancellationToken cancellationToken)
    {
        if (condition.IsRunning)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return new ConditionExecutionResult
            {
                ConditionId = condition.Model.Id,
                Outcome = condition.LastOutcome ?? CheckOutcome.PassedWithWarning,
                Message = Strings.ConditionAlreadyRunning,
                Duration = TimeSpan.Zero,
                RecommendedAction = condition.LastRecommendedAction
            };
        }

        condition.ClearResult();
        condition.IsRunning = true;
        condition.LastMessage = Strings.Running;

        var stopwatch = Stopwatch.StartNew();
        try
        {
            if (condition.RequiresElevation && !IsProcessElevated())
            {
                condition.ApplyElevationRequiredResult();
                return new ConditionExecutionResult
                {
                    ConditionId = condition.Model.Id,
                    Outcome = CheckOutcome.Warning,
                    Message = Strings.ElevationRequiredMessage,
                    Duration = TimeSpan.Zero,
                    RecommendedAction = condition.LastRecommendedAction
                };
            }

            var result = await _conditionExecutor.ExecuteAsync(condition.ToModel(), cancellationToken).ConfigureAwait(true);
            condition.ApplyResult(result);
            return result;
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            var result = new ConditionExecutionResult
            {
                ConditionId = condition.Model.Id,
                Outcome = CheckOutcome.Cancelled,
                Message = Strings.CancelledMessage,
                Duration = stopwatch.Elapsed,
                RecommendedAction = condition.RecommendedAction
            };
            condition.ApplyResult(result);
            return result;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            stopwatch.Stop();
            var result = ConditionExecutionResult.ExecutionError(
                condition.Model.Id,
                exception.Message,
                stopwatch.Elapsed,
                condition.RecommendedAction,
                [exception.Message]);
            condition.ApplyResult(result);
            return result;
        }
        finally
        {
            condition.IsRunning = false;
            ReevaluateStatuses();
        }
    }

    private async Task RunConditionsInGroupOrderAsync(
        GroupEditorViewModel group,
        CancellationToken cancellationToken)
    {
        var parallelBatch = new List<ConditionEditorViewModel>();

        foreach (var condition in group.Conditions)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (condition.RunInParallel)
            {
                parallelBatch.Add(condition);
                continue;
            }

            await RunParallelBatchAsync(parallelBatch, cancellationToken).ConfigureAwait(true);
            await RunRegisteredConditionForUiAsync(condition, cancellationToken).ConfigureAwait(true);
        }

        await RunParallelBatchAsync(parallelBatch, cancellationToken).ConfigureAwait(true);
    }

    private async Task RunParallelBatchAsync(
        List<ConditionEditorViewModel> conditions,
        CancellationToken cancellationToken)
    {
        if (conditions.Count == 0)
        {
            return;
        }

        var tasks = conditions
            .Where(condition => !condition.IsRunning)
            .Select(condition => RunRegisteredConditionForUiAsync(condition, cancellationToken))
            .ToList();
        conditions.Clear();

        await Task.WhenAll(tasks).ConfigureAwait(true);
        cancellationToken.ThrowIfCancellationRequested();
    }

    private bool HasRunningConditionOutsideGroup(GroupEditorViewModel currentGroup) =>
        Groups
            .Where(group => group != currentGroup)
            .SelectMany(group => group.Conditions)
            .Any(condition => condition.IsRunning);

    private async Task<ConditionExecutionResult> RunRegisteredConditionForUiAsync(
        ConditionEditorViewModel condition,
        CancellationToken parentCancellationToken)
    {
        using var cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(parentCancellationToken);
        _conditionRunTokens[condition] = cancellationTokenSource;

        try
        {
            return await RunConditionForUiAsync(condition, cancellationTokenSource.Token).ConfigureAwait(true);
        }
        finally
        {
            _conditionRunTokens.Remove(condition);
        }
    }

    private string BuildOverallMessage()
    {
        var conditions = Groups.SelectMany(group => group.Conditions).ToList();
        if (conditions.All(condition => !condition.LastOutcome.HasValue))
        {
            return Strings.DefinitionsHaveNotBeenRun;
        }

        var errorCount = conditions.Count(condition => condition.LastOutcome == CheckOutcome.Error);
        var cancelledCount = conditions.Count(condition => condition.LastOutcome == CheckOutcome.Cancelled);
        var warningCount = conditions.Count(condition => condition.LastOutcome is CheckOutcome.Warning or CheckOutcome.PassedWithWarning);

        if (errorCount > 0)
        {
            return Format(Strings.ConditionReturnedErrorFormat, errorCount);
        }

        if (cancelledCount > 0)
        {
            return Format(Strings.ConditionCancelledFormat, cancelledCount);
        }

        if (warningCount > 0)
        {
            return Format(Strings.ConditionReturnedWarningsFormat, warningCount);
        }

        return Strings.AllExecutedConditionsPassed;
    }

    private void ReevaluateStatuses()
    {
        foreach (var group in Groups)
        {
            var outcome = Aggregate(group.Conditions.Select(condition => condition.LastOutcome));
            if (outcome.HasValue || !group.LastOutcome.HasValue)
            {
                group.LastOutcome = outcome;
            }

            group.OnConditionStatusesChanged();
        }

        OverallOutcome = Aggregate(Groups.Select(group => group.LastOutcome));
        OverallMessage = BuildOverallMessage();
        NotifyResultsChanged();
    }

    private SavedRunResultDocument BuildSavedResultDocument() =>
        new()
        {
            SavedAt = DateTimeOffset.UtcNow,
            ApplicationName = BrandTitle,
            DefinitionPath = DefinitionPath,
            OverallOutcome = OverallOutcome,
            OverallMessage = OverallMessage,
            Groups = Groups.Select(group => new SavedGroupResult
            {
                Id = group.Id,
                Name = group.Name,
                Description = group.Description,
                Enabled = group.Enabled,
                Outcome = group.LastOutcome,
                Message = group.LastMessage,
                Conditions = group.Conditions.Select(condition => new SavedConditionResult
                {
                    Id = condition.Model.Id,
                    Version = condition.Version,
                    Name = condition.Name,
                    Description = condition.Description,
                    Type = condition.Type,
                    Enabled = condition.Enabled,
                    RequiresElevation = condition.RequiresElevation,
                    Outcome = condition.LastOutcome,
                    Message = condition.LastMessage,
                    MatchedRule = condition.MatchedRule,
                    Duration = condition.Duration,
                    RecommendedAction = condition.LastRecommendedAction,
                    Warnings = SplitLines(condition.WarningSummary),
                    Errors = SplitLines(condition.ErrorSummary),
                    RawOutput = condition.RawOutput
                }).ToList()
            }).ToList()
        };

    private static List<string> SplitLines(string value) =>
        string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).ToList();

    private static CheckOutcome? Aggregate(IEnumerable<CheckOutcome?> outcomes)
    {
        var values = outcomes.Where(value => value.HasValue).Select(value => value!.Value).ToList();
        if (values.Count == 0)
        {
            return null;
        }

        if (values.Contains(CheckOutcome.Error))
        {
            return CheckOutcome.Error;
        }

        if (values.Contains(CheckOutcome.Cancelled))
        {
            return CheckOutcome.Cancelled;
        }

        if (values.Contains(CheckOutcome.Warning))
        {
            return CheckOutcome.Warning;
        }

        if (values.Contains(CheckOutcome.PassedWithWarning))
        {
            return CheckOutcome.PassedWithWarning;
        }

        return CheckOutcome.Passed;
    }

    private async Task RunBusyAsync(Func<CancellationToken, Task> action)
    {
        if (IsBusy)
        {
            return;
        }

        try
        {
            IsBusy = true;
            BeginRun();
            _busyCancellationTokenSource = new CancellationTokenSource();
            await action(_busyCancellationTokenSource.Token).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            StatusMessage = Strings.RunCancelled;
        }
        catch (Exception exception)
        {
            StatusMessage = exception.Message;
        }
        finally
        {
            _busyCancellationTokenSource?.Dispose();
            _busyCancellationTokenSource = null;
            EndRun();
        }
    }

    private void BeginRun()
    {
        _activeRunCount++;
        IsBusy = _activeRunCount > 0;
    }

    private void EndRun()
    {
        _activeRunCount = Math.Max(0, _activeRunCount - 1);
        IsBusy = _activeRunCount > 0;
    }

    private static string FormatValidation(DefinitionValidationResult result)
    {
        var lines = new List<string>();

        if (result.Errors.Count > 0)
        {
            lines.Add(Strings.ErrorsHeader);
            lines.AddRange(result.Errors.Select(error => $" - {error}"));
        }

        if (result.Warnings.Count > 0)
        {
            lines.Add(Strings.WarningsHeader);
            lines.AddRange(result.Warnings.Select(warning => $" - {warning}"));
        }

        return lines.Count == 0 ? Strings.ValidationPassed : string.Join(Environment.NewLine, lines);
    }

    private static bool MoveSelected<T>(
        ObservableCollection<T> collection,
        T? selected,
        int direction,
        Action<T> select)
        where T : class
    {
        if (selected is null)
        {
            return false;
        }

        var oldIndex = collection.IndexOf(selected);
        var newIndex = oldIndex + direction;
        if (oldIndex < 0 || newIndex < 0 || newIndex >= collection.Count)
        {
            return false;
        }

        collection.Move(oldIndex, newIndex);
        select(selected);
        return true;
    }

    private static void MoveSelectedRule(ConditionEditorViewModel condition, RuleEditorViewModel? selected, int direction)
    {
        if (selected is null)
        {
            return;
        }

        var oldIndex = condition.Rules.IndexOf(selected);
        var newIndex = oldIndex + direction;
        if (oldIndex < 0 || newIndex < 0 || newIndex >= condition.Rules.Count)
        {
            return;
        }

        condition.MoveRule(oldIndex, newIndex);
        condition.SelectedRule = selected;
    }

    private void OnSelectionChanged()
    {
        OnPropertyChanged(nameof(HasSelectedGroup));
        OnPropertyChanged(nameof(HasSelectedCondition));
        OnPropertyChanged(nameof(HasSelectedRule));
        OnPropertyChanged(nameof(SelectedCondition));
    }

    private void NotifyResultsChanged()
    {
        OnPropertyChanged(nameof(HasRunResults));
    }

    private void MarkConfigurationChanged() => HasUnsavedChanges = true;

    private string BrandTitle =>
        string.IsNullOrWhiteSpace(BrandName) ? "Prereqqer" : BrandName;

    private void NotifyBrandingChanged()
    {
        OnPropertyChanged(nameof(WindowTitle));
        OnPropertyChanged(nameof(BrandName));
        OnPropertyChanged(nameof(BrandSubtitle));
        OnPropertyChanged(nameof(HeaderBackgroundColor));
        OnPropertyChanged(nameof(HeaderForegroundColor));
        OnPropertyChanged(nameof(AccentColor));
        OnPropertyChanged(nameof(HeaderBackgroundBrush));
        OnPropertyChanged(nameof(HeaderForegroundBrush));
        OnPropertyChanged(nameof(AccentBrush));
    }

    private void NotifyConfigurationSourceChanged()
    {
        OnPropertyChanged(nameof(ConfigurationUrl));
        OnPropertyChanged(nameof(FetchConfigurationOnStartup));
        OnPropertyChanged(nameof(ConfigurationTimeoutSeconds));
        OnPropertyChanged(nameof(HasConfigurationUrl));
    }

    private static IBrush ParseBrush(string value, string fallback)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            try
            {
                return Brush.Parse(value);
            }
            catch (FormatException)
            {
            }
        }

        return Brush.Parse(fallback);
    }

    private static string FormatDuration(TimeSpan duration) =>
        duration.TotalMilliseconds < 1000
            ? $"{duration.TotalMilliseconds:0} ms"
            : $"{duration.TotalSeconds:0.0} s";

    private static string Format(string format, params object[] args) =>
        string.Format(CultureInfo.CurrentUICulture, format, args);

    private static bool IsProcessElevated()
    {
        if (OperatingSystem.IsWindows())
        {
            try
            {
                using var identity = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }

        return geteuid() == 0;
    }

    [DllImport("libc")]
    private static extern uint geteuid();

    private sealed class SavedRunResultDocument
    {
        public int SchemaVersion { get; init; } = 1;

        public DateTimeOffset SavedAt { get; init; }

        public string ApplicationName { get; init; } = string.Empty;

        public string DefinitionPath { get; init; } = string.Empty;

        public CheckOutcome? OverallOutcome { get; init; }

        public string OverallMessage { get; init; } = string.Empty;

        public List<SavedGroupResult> Groups { get; init; } = [];
    }

    private sealed class SavedGroupResult
    {
        public string Id { get; init; } = string.Empty;

        public string Name { get; init; } = string.Empty;

        public string Description { get; init; } = string.Empty;

        public bool Enabled { get; init; }

        public CheckOutcome? Outcome { get; init; }

        public string Message { get; init; } = string.Empty;

        public List<SavedConditionResult> Conditions { get; init; } = [];
    }

    private sealed class SavedConditionResult
    {
        public string Id { get; init; } = string.Empty;

        public int Version { get; init; }

        public string Name { get; init; } = string.Empty;

        public string Description { get; init; } = string.Empty;

        public ConditionType Type { get; init; }

        public bool Enabled { get; init; }

        public bool RequiresElevation { get; init; }

        public CheckOutcome? Outcome { get; init; }

        public string Message { get; init; } = string.Empty;

        public string MatchedRule { get; init; } = string.Empty;

        public string Duration { get; init; } = string.Empty;

        public string RecommendedAction { get; init; } = string.Empty;

        public List<string> Warnings { get; init; } = [];

        public List<string> Errors { get; init; } = [];

        public string RawOutput { get; init; } = string.Empty;
    }
}
