using Prereqqer.Core.Definitions;
using Prereqqer.Core.Execution;

namespace Prereqqer.Core.Tests;

public sealed class PowerShellConditionExecutorTests
{
    [Fact]
    public async Task ExecuteAsync_returns_error_for_parse_failures()
    {
        var condition = CreatePowerShellCondition("if (");

        var result = await new ConditionExecutor().ExecuteAsync(condition);

        Assert.Equal(CheckOutcome.Error, result.Outcome);
        Assert.Contains("parsed", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public async Task ExecuteAsync_returns_error_when_script_times_out()
    {
        var condition = CreatePowerShellCondition("Start-Sleep -Seconds 5");
        condition.TimeoutSeconds = 1;

        var result = await new ConditionExecutor().ExecuteAsync(condition);

        Assert.Equal(CheckOutcome.Error, result.Outcome);
        Assert.Contains("timed out", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_normalizes_structured_output_and_applies_rules()
    {
        var condition = CreatePowerShellCondition("[pscustomobject]@{ Status = 'Ok'; Version = 9 }");
        condition.Rules =
        [
            new OutcomeRuleDefinition
            {
                Name = "Status is ok",
                Outcome = CheckOutcome.PassedWithWarning,
                Target = RuleTarget.FirstRow,
                Field = "Status",
                Operator = RuleOperator.Equals,
                ExpectedValue = "Ok",
                RecommendedAction = "Keep the current runtime."
            }
        ];

        var result = await new ConditionExecutor().ExecuteAsync(condition);

        Assert.Equal(CheckOutcome.PassedWithWarning, result.Outcome);
        Assert.Equal("Status is ok", result.MatchedRuleName);
        Assert.Single(result.Data.Rows);
        Assert.Equal("Ok", result.Data.Rows[0]["Status"]);
        Assert.Equal(9, result.Data.Scalars["Version"]);
        Assert.Equal("Keep the current runtime.", result.RecommendedAction);
    }

    [Fact]
    public async Task ExecuteAsync_uses_condition_recommended_action_when_matching_rule_has_no_action()
    {
        var condition = CreatePowerShellCondition("[pscustomobject]@{ Status = 'Warning' }");
        condition.RecommendedAction = "Review the script result.";
        condition.Rules =
        [
            new OutcomeRuleDefinition
            {
                Name = "Warning status",
                Outcome = CheckOutcome.Warning,
                Target = RuleTarget.FirstRow,
                Field = "Status",
                Operator = RuleOperator.Equals,
                ExpectedValue = "Warning"
            }
        ];

        var result = await new ConditionExecutor().ExecuteAsync(condition);

        Assert.Equal(CheckOutcome.Warning, result.Outcome);
        Assert.Equal("Review the script result.", result.RecommendedAction);
    }

    [Fact]
    public async Task ExecuteAsync_defaults_to_passed_with_warning_when_warning_stream_has_entries()
    {
        var condition = CreatePowerShellCondition("Write-Warning 'check warning'; [pscustomobject]@{ Status = 'Ok' }");

        var result = await new ConditionExecutor().ExecuteAsync(condition);

        Assert.Equal(CheckOutcome.PassedWithWarning, result.Outcome);
        Assert.Contains("check warning", result.Warnings);
    }

    private static ConditionDefinition CreatePowerShellCondition(string script) =>
        new()
        {
            Name = "test script",
            Type = ConditionType.PowerShell,
            TimeoutSeconds = 10,
            PowerShell = new PowerShellCheckDefinition
            {
                Script = script
            },
            Rules = []
        };
}
