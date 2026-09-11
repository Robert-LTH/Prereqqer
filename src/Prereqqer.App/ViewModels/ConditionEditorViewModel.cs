using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Prereqqer.App.Resources;
using Prereqqer.Core.Definitions;
using Prereqqer.Core.Execution;

namespace Prereqqer.App.ViewModels;

public sealed partial class ConditionEditorViewModel : ViewModelBase
{
    private readonly Action? _onDefinitionChanged;

    [ObservableProperty]
    private RuleEditorViewModel? _selectedRule;

    [ObservableProperty]
    private CheckOutcome? _lastOutcome;

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private bool _isExpanded;

    [ObservableProperty]
    private bool _isGroupRunning;

    [ObservableProperty]
    private string _lastMessage = Strings.NotRunMessage;

    [ObservableProperty]
    private string _matchedRule = string.Empty;

    [ObservableProperty]
    private string _duration = string.Empty;

    [ObservableProperty]
    private string _rawOutput = string.Empty;

    [ObservableProperty]
    private string _warningSummary = string.Empty;

    [ObservableProperty]
    private string _errorSummary = string.Empty;

    [ObservableProperty]
    private string _lastRecommendedAction = string.Empty;

    [ObservableProperty]
    private string _validationSummary = string.Empty;

    [ObservableProperty]
    private string _statusOverride = string.Empty;

    public ConditionEditorViewModel(ConditionDefinition model, Action? onDefinitionChanged = null)
    {
        Model = model;
        _onDefinitionChanged = onDefinitionChanged;
        Rules = new ObservableCollection<RuleEditorViewModel>(
            model.Rules.Select(rule => new RuleEditorViewModel(rule, MarkDefinitionChanged)));
        SelectedRule = Rules.FirstOrDefault();
    }

    public ConditionDefinition Model { get; }

    public ObservableCollection<RuleEditorViewModel> Rules { get; }

    public static IReadOnlyList<ConditionType> TypeOptions { get; } = Enum.GetValues<ConditionType>();

    public string Name
    {
        get => Model.Name;
        set => SetDefinitionProperty(Model.Name, value, Model, static (model, newValue) => model.Name = newValue);
    }

    public string Description
    {
        get => Model.Description;
        set => SetDefinitionProperty(Model.Description, value, Model, static (model, newValue) => model.Description = newValue);
    }

    public string RecommendedAction
    {
        get => Model.RecommendedAction;
        set => SetDefinitionProperty(Model.RecommendedAction, value, Model, static (model, newValue) => model.RecommendedAction = newValue);
    }

    public bool RequiresElevation
    {
        get => Model.RequiresElevation;
        set => SetDefinitionProperty(Model.RequiresElevation, value, Model, static (model, newValue) => model.RequiresElevation = newValue);
    }

    public bool Enabled
    {
        get => Model.Enabled;
        set => SetDefinitionProperty(Model.Enabled, value, Model, static (model, newValue) => model.Enabled = newValue);
    }

    public bool RunInParallel
    {
        get => Model.RunInParallel;
        set => SetDefinitionProperty(Model.RunInParallel, value, Model, static (model, newValue) => model.RunInParallel = newValue);
    }

    public ConditionType Type
    {
        get => Model.Type;
        set
        {
            if (SetDefinitionProperty(Model.Type, value, Model, static (model, newValue) => model.Type = newValue))
            {
                OnPropertyChanged(nameof(IsWmi));
                OnPropertyChanged(nameof(IsPowerShell));
            }
        }
    }

    public int TimeoutSeconds
    {
        get => Model.TimeoutSeconds;
        set => SetDefinitionProperty(Model.TimeoutSeconds, value, Model, static (model, newValue) => model.TimeoutSeconds = Math.Clamp(newValue, 1, 3600));
    }

    public string WmiNamespace
    {
        get => Model.Wmi.Namespace;
        set => SetDefinitionProperty(Model.Wmi.Namespace, value, Model.Wmi, static (model, newValue) => model.Namespace = newValue);
    }

    public string WmiQuery
    {
        get => Model.Wmi.Query;
        set => SetDefinitionProperty(Model.Wmi.Query, value, Model.Wmi, static (model, newValue) => model.Query = newValue);
    }

    public string PowerShellScript
    {
        get => Model.PowerShell.Script;
        set => SetDefinitionProperty(Model.PowerShell.Script, value, Model.PowerShell, static (model, newValue) => model.Script = newValue);
    }

    public int Version => Model.Version;

    public bool IsWmi => Type == ConditionType.Wmi;

    public bool IsPowerShell => Type == ConditionType.PowerShell;

    public bool HasSelectedRule => SelectedRule is not null;

    public bool HasLastRecommendedAction => !string.IsNullOrWhiteSpace(LastRecommendedAction);

    public bool ShowRecommendedAction => !IsRunning
        && LastOutcome.HasValue
        && LastOutcome != CheckOutcome.Passed
        && HasLastRecommendedAction;

    public bool ShowRunButton => !IsRunning && !IsGroupRunning;

    public string LastOutcomeDisplay => string.IsNullOrWhiteSpace(StatusOverride)
        ? OutcomePresentation.ToDisplayName(LastOutcome)
        : StatusOverride;

    public string CurrentOutcomeDisplay => IsRunning ? Strings.Running : LastOutcomeDisplay;

    public string ExpansionSymbol => IsExpanded ? "-" : "+";

    public IBrush OutcomeBackgroundBrush => OutcomePresentation.ToBackgroundBrush(LastOutcome);

    public IBrush OutcomeBorderBrush => OutcomePresentation.ToBorderBrush(LastOutcome);

    public IBrush OutcomeForegroundBrush => OutcomePresentation.ToForegroundBrush(LastOutcome);

    public Geometry StatusIconGeometry => OutcomePresentation.ToStatusIconGeometry(LastOutcome);

    public void AddRule(RuleEditorViewModel rule)
    {
        Rules.Add(rule);
        MarkDefinitionChanged();
    }

    public void RemoveRule(RuleEditorViewModel rule)
    {
        if (Rules.Remove(rule))
        {
            MarkDefinitionChanged();
        }
    }

    public void MoveRule(int oldIndex, int newIndex)
    {
        Rules.Move(oldIndex, newIndex);
        MarkDefinitionChanged();
    }

    public void MarkDefinitionChanged()
    {
        Model.Version = Math.Max(1, Model.Version) + 1;
        OnPropertyChanged(nameof(Version));
        _onDefinitionChanged?.Invoke();
    }

    partial void OnSelectedRuleChanged(RuleEditorViewModel? value)
    {
        OnPropertyChanged(nameof(HasSelectedRule));
    }

    partial void OnLastOutcomeChanged(CheckOutcome? value)
    {
        OnPropertyChanged(nameof(LastOutcomeDisplay));
        OnPropertyChanged(nameof(CurrentOutcomeDisplay));
        OnPropertyChanged(nameof(ShowRecommendedAction));
        OnPropertyChanged(nameof(OutcomeBackgroundBrush));
        OnPropertyChanged(nameof(OutcomeBorderBrush));
        OnPropertyChanged(nameof(OutcomeForegroundBrush));
        OnPropertyChanged(nameof(StatusIconGeometry));
    }

    partial void OnIsRunningChanged(bool value)
    {
        OnPropertyChanged(nameof(CurrentOutcomeDisplay));
        OnPropertyChanged(nameof(ShowRecommendedAction));
        OnPropertyChanged(nameof(ShowRunButton));
    }

    partial void OnIsExpandedChanged(bool value)
    {
        OnPropertyChanged(nameof(ExpansionSymbol));
    }

    partial void OnIsGroupRunningChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowRunButton));
    }

    partial void OnLastRecommendedActionChanged(string value)
    {
        OnPropertyChanged(nameof(HasLastRecommendedAction));
        OnPropertyChanged(nameof(ShowRecommendedAction));
    }

    partial void OnStatusOverrideChanged(string value)
    {
        OnPropertyChanged(nameof(LastOutcomeDisplay));
        OnPropertyChanged(nameof(CurrentOutcomeDisplay));
    }

    public ConditionDefinition ToModel()
    {
        Model.Rules = Rules.Select(rule => rule.ToModel()).ToList();
        return Model;
    }

    public void ApplyResult(ConditionExecutionResult result)
    {
        LastOutcome = result.Outcome;
        LastMessage = result.Message;
        MatchedRule = string.IsNullOrWhiteSpace(result.MatchedRuleName) ? string.Empty : result.MatchedRuleName;
        Duration = result.Duration.TotalMilliseconds < 1000
            ? $"{result.Duration.TotalMilliseconds:0} ms"
            : $"{result.Duration.TotalSeconds:0.0} s";
        RawOutput = result.RawData;
        WarningSummary = string.Join(Environment.NewLine, result.Warnings);
        ErrorSummary = string.Join(Environment.NewLine, result.Errors);
        LastRecommendedAction = result.RecommendedAction;
    }

    public void CopyRuntimeStateFrom(ConditionEditorViewModel source)
    {
        LastOutcome = source.LastOutcome;
        IsRunning = false;
        IsExpanded = source.IsExpanded;
        LastMessage = source.LastMessage;
        MatchedRule = source.MatchedRule;
        Duration = source.Duration;
        RawOutput = source.RawOutput;
        WarningSummary = source.WarningSummary;
        ErrorSummary = source.ErrorSummary;
        LastRecommendedAction = source.LastRecommendedAction;
        ValidationSummary = source.ValidationSummary;
        StatusOverride = source.StatusOverride;
    }

    public void ApplyElevationRequiredResult()
    {
        ApplyResult(new ConditionExecutionResult
        {
            ConditionId = Model.Id,
            Outcome = CheckOutcome.Warning,
            Message = Strings.ElevationRequiredMessage,
            Duration = TimeSpan.Zero,
            RecommendedAction = Strings.ElevationRequiredRecommendedAction
        });
        StatusOverride = Strings.ElevationRequiredMessage;
    }

    public void ClearResult()
    {
        IsRunning = false;
        LastOutcome = null;
        LastMessage = Strings.NotRunMessage;
        MatchedRule = string.Empty;
        Duration = string.Empty;
        RawOutput = string.Empty;
        WarningSummary = string.Empty;
        ErrorSummary = string.Empty;
        LastRecommendedAction = string.Empty;
        StatusOverride = string.Empty;
    }

    private bool SetDefinitionProperty<TModel, TValue>(
        TValue oldValue,
        TValue newValue,
        TModel model,
        Action<TModel, TValue> callback)
        where TModel : class
    {
        if (!SetProperty(oldValue, newValue, model, callback))
        {
            return false;
        }

        MarkDefinitionChanged();
        return true;
    }
}
