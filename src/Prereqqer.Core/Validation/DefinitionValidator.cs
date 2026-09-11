using Prereqqer.Core.Definitions;
using Prereqqer.Core.Execution;

namespace Prereqqer.Core.Validation;

public sealed class DefinitionValidator
{
    public DefinitionValidationResult ValidateDocument(ConditionDefinitionDocument document)
    {
        var result = new DefinitionValidationResult();

        if (document.SchemaVersion != 1)
        {
            result.Warnings.Add($"Schema version {document.SchemaVersion} is not explicitly supported; attempting v1-compatible handling.");
        }

        if (document.Groups.Count == 0)
        {
            result.Warnings.Add("No groups are defined.");
        }

        foreach (var group in document.Groups)
        {
            if (string.IsNullOrWhiteSpace(group.Name))
            {
                result.Errors.Add("A group is missing its name.");
            }

            foreach (var condition in group.Conditions)
            {
                Merge(result, ValidateCondition(condition));
            }
        }

        return result;
    }

    public DefinitionValidationResult ValidateCondition(ConditionDefinition condition)
    {
        var result = new DefinitionValidationResult();

        if (string.IsNullOrWhiteSpace(condition.Name))
        {
            result.Errors.Add("Condition name is required.");
        }

        if (condition.TimeoutSeconds < 1)
        {
            result.Errors.Add("Timeout must be at least 1 second.");
        }

        if (condition.Rules.Count == 0)
        {
            result.Warnings.Add("No outcome rules are configured; successful execution will default to Passed.");
        }

        foreach (var rule in condition.Rules)
        {
            if (string.IsNullOrWhiteSpace(rule.Name))
            {
                result.Errors.Add("A rule is missing its name.");
            }

            if (rule.Outcome == CheckOutcome.Cancelled)
            {
                result.Errors.Add($"Rule '{rule.Name}' cannot use Cancelled as an outcome.");
            }

            if (rule.Target != RuleTarget.RowCount && string.IsNullOrWhiteSpace(rule.Field))
            {
                result.Errors.Add($"Rule '{rule.Name}' requires a field for target {rule.Target}.");
            }
        }

        switch (condition.Type)
        {
            case ConditionType.PowerShell:
                ValidatePowerShell(condition, result);
                break;
            case ConditionType.Wmi:
                ValidateWmi(condition, result);
                break;
            default:
                result.Errors.Add("Unsupported condition type.");
                break;
        }

        return result;
    }

    private static void ValidatePowerShell(ConditionDefinition condition, DefinitionValidationResult result)
    {
        if (string.IsNullOrWhiteSpace(condition.PowerShell.Script))
        {
            result.Errors.Add("PowerShell script is required.");
            return;
        }

        foreach (var error in PowerShellScriptValidator.GetParseErrors(condition.PowerShell.Script))
        {
            result.Errors.Add($"PowerShell parse error: {error}");
        }
    }

    private static void ValidateWmi(ConditionDefinition condition, DefinitionValidationResult result)
    {
        if (string.IsNullOrWhiteSpace(condition.Wmi.Namespace))
        {
            result.Errors.Add("WMI namespace is required.");
        }

        if (string.IsNullOrWhiteSpace(condition.Wmi.Query))
        {
            result.Errors.Add("WMI query is required.");
        }

        if (!condition.Wmi.Query.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
        {
            result.Warnings.Add("WMI query does not start with SELECT.");
        }
    }

    private static void Merge(DefinitionValidationResult target, DefinitionValidationResult source)
    {
        target.Errors.AddRange(source.Errors);
        target.Warnings.AddRange(source.Warnings);
    }
}
