using Prereqqer.Core.Definitions;
using Prereqqer.Core.Execution;
using Prereqqer.Core.Rules;
using Prereqqer.Core.Validation;

namespace Prereqqer.Core.Tests;

public sealed class RuleEvaluatorTests
{
    [Theory]
    [InlineData(CheckOutcome.Passed, "ok")]
    [InlineData(CheckOutcome.PassedWithWarning, "soft-pass")]
    [InlineData(CheckOutcome.Warning, "warn")]
    [InlineData(CheckOutcome.Error, "error")]
    public void Evaluate_returns_first_matching_outcome(CheckOutcome outcome, string expectedStatus)
    {
        var evaluator = new RuleEvaluator();
        var data = new CheckDataSet
        {
            Rows =
            [
                new Dictionary<string, object?>
                {
                    ["Status"] = expectedStatus
                }
            ]
        };
        var rules = new List<OutcomeRuleDefinition>
        {
            new()
            {
                Name = outcome.ToString(),
                Outcome = outcome,
                Target = RuleTarget.FirstRow,
                Field = "Status",
                Operator = RuleOperator.Equals,
                ExpectedValue = expectedStatus,
                RecommendedAction = "Follow the matched remediation guidance."
            }
        };

        var result = evaluator.Evaluate(rules, data);

        Assert.Equal(outcome, result.Outcome);
        Assert.Equal(outcome.ToString(), result.MatchedRuleName);
        Assert.Equal("Follow the matched remediation guidance.", result.RecommendedAction);
    }

    [Fact]
    public void Evaluate_supports_row_count_any_row_all_rows_and_scalar_rules()
    {
        var evaluator = new RuleEvaluator();
        var data = new CheckDataSet
        {
            Rows =
            [
                new Dictionary<string, object?> { ["Score"] = 10, ["State"] = "Ready" },
                new Dictionary<string, object?> { ["Score"] = 20, ["State"] = "Ready" }
            ],
            Scalars = new Dictionary<string, object?>
            {
                ["Value"] = "complete"
            }
        };

        Assert.True(evaluator.Matches(new OutcomeRuleDefinition
        {
            Target = RuleTarget.RowCount,
            Operator = RuleOperator.Equals,
            ExpectedValue = "2"
        }, data));
        Assert.True(evaluator.Matches(new OutcomeRuleDefinition
        {
            Target = RuleTarget.AnyRow,
            Field = "Score",
            Operator = RuleOperator.GreaterThan,
            ExpectedValue = "15"
        }, data));
        Assert.True(evaluator.Matches(new OutcomeRuleDefinition
        {
            Target = RuleTarget.AllRows,
            Field = "State",
            Operator = RuleOperator.Equals,
            ExpectedValue = "Ready"
        }, data));
        Assert.True(evaluator.Matches(new OutcomeRuleDefinition
        {
            Target = RuleTarget.Scalar,
            Field = "Value",
            Operator = RuleOperator.RegexMatch,
            ExpectedValue = "comp.*"
        }, data));
    }

    [Fact]
    public void ValidateCondition_rejects_cancelled_rule_outcome()
    {
        var validator = new DefinitionValidator();
        var condition = DefinitionFactory.CreateDefaultPowerShellCondition();
        condition.Rules =
        [
            new OutcomeRuleDefinition
            {
                Name = "Invalid runtime outcome",
                Outcome = CheckOutcome.Cancelled,
                Target = RuleTarget.RowCount,
                Operator = RuleOperator.GreaterThan,
                ExpectedValue = "0"
            }
        ];

        var result = validator.ValidateCondition(condition);

        Assert.Contains(result.Errors, error => error.Contains("cannot use Cancelled", StringComparison.Ordinal));
    }
}
