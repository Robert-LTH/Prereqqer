using CommunityToolkit.Mvvm.ComponentModel;
using Prereqqer.Core.Definitions;

namespace Prereqqer.App.ViewModels;

public sealed class RuleEditorViewModel : ViewModelBase
{
    private readonly Action? _onDefinitionChanged;

    public RuleEditorViewModel(OutcomeRuleDefinition model, Action? onDefinitionChanged = null)
    {
        Model = model;
        _onDefinitionChanged = onDefinitionChanged;
    }

    public OutcomeRuleDefinition Model { get; }

    public static IReadOnlyList<CheckOutcome> OutcomeOptions { get; } =
        Enum.GetValues<CheckOutcome>()
            .Where(outcome => outcome != CheckOutcome.Cancelled)
            .ToList();

    public static IReadOnlyList<RuleTarget> TargetOptions { get; } = Enum.GetValues<RuleTarget>();

    public static IReadOnlyList<RuleOperator> OperatorOptions { get; } = Enum.GetValues<RuleOperator>();

    public string Name
    {
        get => Model.Name;
        set => SetDefinitionProperty(Model.Name, value, Model, static (model, newValue) => model.Name = newValue);
    }

    public CheckOutcome Outcome
    {
        get => Model.Outcome;
        set => SetDefinitionProperty(Model.Outcome, value, Model, static (model, newValue) => model.Outcome = newValue);
    }

    public RuleTarget Target
    {
        get => Model.Target;
        set => SetDefinitionProperty(Model.Target, value, Model, static (model, newValue) => model.Target = newValue);
    }

    public string Field
    {
        get => Model.Field;
        set => SetDefinitionProperty(Model.Field, value, Model, static (model, newValue) => model.Field = newValue);
    }

    public RuleOperator Operator
    {
        get => Model.Operator;
        set => SetDefinitionProperty(Model.Operator, value, Model, static (model, newValue) => model.Operator = newValue);
    }

    public string ExpectedValue
    {
        get => Model.ExpectedValue;
        set => SetDefinitionProperty(Model.ExpectedValue, value, Model, static (model, newValue) => model.ExpectedValue = newValue);
    }

    public string RecommendedAction
    {
        get => Model.RecommendedAction;
        set => SetDefinitionProperty(Model.RecommendedAction, value, Model, static (model, newValue) => model.RecommendedAction = newValue);
    }

    public OutcomeRuleDefinition ToModel() => Model;

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

        _onDefinitionChanged?.Invoke();
        return true;
    }
}
