using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Prereqqer.App.Resources;
using Prereqqer.Core.Definitions;

namespace Prereqqer.App.ViewModels;

public sealed partial class GroupEditorViewModel : ViewModelBase
{
    private readonly Action? _onDefinitionChanged;

    [ObservableProperty]
    private ConditionEditorViewModel? _selectedCondition;

    [ObservableProperty]
    private CheckOutcome? _lastOutcome;

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private bool _isExpanded = true;

    [ObservableProperty]
    private string _lastMessage = Strings.NotRunMessage;

    public GroupEditorViewModel(ConditionGroupDefinition model, Action? onDefinitionChanged = null)
    {
        Model = model;
        _onDefinitionChanged = onDefinitionChanged;
        Conditions = new ObservableCollection<ConditionEditorViewModel>(
            model.Conditions.Select(condition => new ConditionEditorViewModel(condition, MarkDefinitionChanged)));
    }

    public ConditionGroupDefinition Model { get; }

    public ObservableCollection<ConditionEditorViewModel> Conditions { get; }

    public string Id => Model.Id;

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

    public bool Enabled
    {
        get => Model.Enabled;
        set
        {
            if (SetDefinitionProperty(Model.Enabled, value, Model, static (model, newValue) => model.Enabled = newValue))
            {
                OnPropertyChanged(nameof(DisplayOpacity));
                OnPropertyChanged(nameof(IsDisabled));
            }
        }
    }

    public bool IsDisabled => !Enabled;

    public double DisplayOpacity => Enabled ? 1.0 : 0.52;

    public string LastOutcomeDisplay => OutcomePresentation.ToDisplayName(LastOutcome);

    public string CurrentOutcomeDisplay => IsRunning ? Strings.Running : LastOutcomeDisplay;

    public string FailedConditionsTooltip
    {
        get
        {
            var failedConditions = Conditions
                .Where(condition => condition.LastOutcome.HasValue && condition.LastOutcome != CheckOutcome.Passed)
                .Select(condition => $"{condition.Name}: {condition.LastOutcomeDisplay}")
                .ToList();

            return failedConditions.Count == 0
                ? Strings.NoChecksFailed
                : string.Join(Environment.NewLine, failedConditions);
        }
    }

    public IBrush OutcomeBackgroundBrush => OutcomePresentation.ToBackgroundBrush(LastOutcome);

    public IBrush OutcomeBorderBrush => OutcomePresentation.ToBorderBrush(LastOutcome);

    public IBrush OutcomeForegroundBrush => OutcomePresentation.ToForegroundBrush(LastOutcome);

    public string ExpansionSymbol => IsExpanded ? "-" : "+";

    partial void OnLastOutcomeChanged(CheckOutcome? value)
    {
        OnPropertyChanged(nameof(LastOutcomeDisplay));
        OnPropertyChanged(nameof(CurrentOutcomeDisplay));
        OnPropertyChanged(nameof(OutcomeBackgroundBrush));
        OnPropertyChanged(nameof(OutcomeBorderBrush));
        OnPropertyChanged(nameof(OutcomeForegroundBrush));
        OnPropertyChanged(nameof(FailedConditionsTooltip));
    }

    partial void OnIsRunningChanged(bool value)
    {
        OnPropertyChanged(nameof(CurrentOutcomeDisplay));

        foreach (var condition in Conditions)
        {
            condition.IsGroupRunning = value;
        }
    }

    partial void OnIsExpandedChanged(bool value)
    {
        OnPropertyChanged(nameof(ExpansionSymbol));
    }

    public ConditionGroupDefinition ToModel()
    {
        Model.Conditions = Conditions.Select(condition => condition.ToModel()).ToList();
        return Model;
    }

    public void ClearResult()
    {
        IsRunning = false;
        LastOutcome = null;
        LastMessage = Strings.NotRunMessage;

        foreach (var condition in Conditions)
        {
            condition.ClearResult();
        }

        OnConditionStatusesChanged();
    }

    public void OnConditionStatusesChanged()
    {
        OnPropertyChanged(nameof(FailedConditionsTooltip));
    }

    private void MarkDefinitionChanged() => _onDefinitionChanged?.Invoke();

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
