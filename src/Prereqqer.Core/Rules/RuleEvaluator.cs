using System.Globalization;
using System.Text.RegularExpressions;
using Prereqqer.Core.Definitions;
using Prereqqer.Core.Execution;

namespace Prereqqer.Core.Rules;

public sealed class RuleEvaluator
{
    public RuleEvaluationResult Evaluate(
        IReadOnlyList<OutcomeRuleDefinition> rules,
        CheckDataSet data,
        bool hasWarnings = false)
    {
        foreach (var rule in rules)
        {
            if (Matches(rule, data))
            {
                return new RuleEvaluationResult
                {
                    Outcome = rule.Outcome,
                    MatchedRuleName = rule.Name,
                    RecommendedAction = rule.RecommendedAction,
                    Message = $"Matched rule: {rule.Name}"
                };
            }
        }

        return new RuleEvaluationResult
        {
            Outcome = hasWarnings ? CheckOutcome.PassedWithWarning : CheckOutcome.Passed,
            Message = hasWarnings
                ? "No rules matched; check completed with warnings."
                : "No rules matched; check completed successfully."
        };
    }

    public bool Matches(OutcomeRuleDefinition rule, CheckDataSet data) =>
        rule.Target switch
        {
            RuleTarget.RowCount => Compare(data.RowCount, rule.Operator, rule.ExpectedValue),
            RuleTarget.FirstRow => MatchFirstRow(rule, data),
            RuleTarget.AnyRow => data.Rows.Any(row => Compare(GetFieldValue(row, rule.Field), rule.Operator, rule.ExpectedValue)),
            RuleTarget.AllRows => data.Rows.Count > 0 && data.Rows.All(row => Compare(GetFieldValue(row, rule.Field), rule.Operator, rule.ExpectedValue)),
            RuleTarget.Scalar => Compare(GetFieldValue(data.Scalars, rule.Field), rule.Operator, rule.ExpectedValue),
            _ => false
        };

    private static bool MatchFirstRow(OutcomeRuleDefinition rule, CheckDataSet data)
    {
        if (data.Rows.Count == 0)
        {
            return rule.Operator == RuleOperator.NotExists;
        }

        return Compare(GetFieldValue(data.Rows[0], rule.Field), rule.Operator, rule.ExpectedValue);
    }

    private static object? GetFieldValue(IReadOnlyDictionary<string, object?> values, string field)
    {
        if (string.IsNullOrWhiteSpace(field))
        {
            field = "Value";
        }

        return values.TryGetValue(field, out var value)
            ? value
            : values.FirstOrDefault(item => string.Equals(item.Key, field, StringComparison.OrdinalIgnoreCase)).Value;
    }

    private static bool Compare(object? actual, RuleOperator ruleOperator, string expected)
    {
        var exists = Exists(actual);

        return ruleOperator switch
        {
            RuleOperator.Exists => exists,
            RuleOperator.NotExists => !exists,
            RuleOperator.Equals => string.Equals(ToString(actual), expected, StringComparison.OrdinalIgnoreCase),
            RuleOperator.NotEquals => !string.Equals(ToString(actual), expected, StringComparison.OrdinalIgnoreCase),
            RuleOperator.GreaterThan => TryCompareNumbers(actual, expected, out var greaterThan) && greaterThan > 0,
            RuleOperator.GreaterThanOrEqual => TryCompareNumbers(actual, expected, out var greaterThanOrEqual) && greaterThanOrEqual >= 0,
            RuleOperator.LessThan => TryCompareNumbers(actual, expected, out var lessThan) && lessThan < 0,
            RuleOperator.LessThanOrEqual => TryCompareNumbers(actual, expected, out var lessThanOrEqual) && lessThanOrEqual <= 0,
            RuleOperator.Contains => ToString(actual).Contains(expected, StringComparison.OrdinalIgnoreCase),
            RuleOperator.NotContains => !ToString(actual).Contains(expected, StringComparison.OrdinalIgnoreCase),
            RuleOperator.RegexMatch => Regex.IsMatch(ToString(actual), expected, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
            RuleOperator.RegexNotMatch => !Regex.IsMatch(ToString(actual), expected, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
            _ => false
        };
    }

    private static bool Exists(object? actual) =>
        actual switch
        {
            null => false,
            string text => !string.IsNullOrWhiteSpace(text),
            IEnumerable<object?> values => values.Any(),
            _ => true
        };

    private static string ToString(object? value) => ValueNormalizer.ToDisplayString(value);

    private static bool TryCompareNumbers(object? actual, string expected, out int comparison)
    {
        comparison = 0;

        if (!double.TryParse(ToString(actual), NumberStyles.Float, CultureInfo.InvariantCulture, out var actualNumber)
            || !double.TryParse(expected, NumberStyles.Float, CultureInfo.InvariantCulture, out var expectedNumber))
        {
            return false;
        }

        comparison = actualNumber.CompareTo(expectedNumber);
        return true;
    }
}
