using System.Diagnostics;
using System.Management.Automation;
using System.Management.Automation.Language;
using Prereqqer.Core.Definitions;
using Prereqqer.Core.Rules;

namespace Prereqqer.Core.Execution;

public sealed class PowerShellConditionExecutor
{
    private readonly RuleEvaluator _ruleEvaluator;

    public PowerShellConditionExecutor(RuleEvaluator? ruleEvaluator = null)
    {
        _ruleEvaluator = ruleEvaluator ?? new RuleEvaluator();
    }

    public async Task<ConditionExecutionResult> ExecuteAsync(
        ConditionDefinition condition,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        var started = Stopwatch.GetTimestamp();
        var parseErrors = PowerShellScriptValidator.GetParseErrors(condition.PowerShell.Script);
        if (parseErrors.Count > 0)
        {
            return ConditionExecutionResult.ExecutionError(
                condition.Id,
                "PowerShell script could not be parsed.",
                Stopwatch.GetElapsedTime(started),
                condition.RecommendedAction,
                parseErrors);
        }

        using var powerShell = PowerShell.Create();
        powerShell.AddScript(condition.PowerShell.Script, useLocalScope: true);

        try
        {
            var invokeTask = Task.Run(() => powerShell.Invoke(), CancellationToken.None);
            var delayTask = Task.Delay(timeout, cancellationToken);
            var completedTask = await Task.WhenAny(invokeTask, delayTask).ConfigureAwait(false);

            if (completedTask != invokeTask)
            {
                powerShell.Stop();

                if (cancellationToken.IsCancellationRequested)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }

                return ConditionExecutionResult.ExecutionError(
                    condition.Id,
                    $"PowerShell script timed out after {timeout.TotalSeconds:0} seconds.",
                    Stopwatch.GetElapsedTime(started),
                    condition.RecommendedAction);
            }

            var output = await invokeTask.ConfigureAwait(false);
            var errors = powerShell.Streams.Error.Select(error => error.ToString()).ToList();
            if (powerShell.HadErrors || errors.Count > 0)
            {
                return ConditionExecutionResult.ExecutionError(
                    condition.Id,
                    "PowerShell script wrote to the error stream.",
                    Stopwatch.GetElapsedTime(started),
                    condition.RecommendedAction,
                    errors);
            }

            var warnings = powerShell.Streams.Warning.Select(warning => warning.Message).ToList();
            var data = NormalizeOutput(output);
            var evaluation = _ruleEvaluator.Evaluate(condition.Rules, data, warnings.Count > 0);

            return new ConditionExecutionResult
            {
                ConditionId = condition.Id,
                Outcome = evaluation.Outcome,
                Message = evaluation.Message,
                MatchedRuleName = evaluation.MatchedRuleName,
                RecommendedAction = ResolveRecommendedAction(condition, evaluation.RecommendedAction),
                Duration = Stopwatch.GetElapsedTime(started),
                Data = data,
                Output = output.Select(item => item?.ToString() ?? string.Empty).ToList(),
                Warnings = warnings
            };
        }
        catch (RuntimeException exception)
        {
            return ConditionExecutionResult.ExecutionError(
                condition.Id,
                exception.Message,
                Stopwatch.GetElapsedTime(started),
                condition.RecommendedAction,
                [exception.ToString()]);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return ConditionExecutionResult.ExecutionError(
                condition.Id,
                exception.Message,
                Stopwatch.GetElapsedTime(started),
                condition.RecommendedAction,
                [exception.ToString()]);
        }
    }

    private static CheckDataSet NormalizeOutput(IEnumerable<PSObject> output)
    {
        var data = new CheckDataSet();

        foreach (var item in output)
        {
            data.Rows.Add(ValueNormalizer.FromPowerShellObject(item));
        }

        if (data.Rows.Count == 1)
        {
            foreach (var item in data.Rows[0])
            {
                data.Scalars[item.Key] = item.Value;
            }
        }

        return data;
    }

    private static string ResolveRecommendedAction(ConditionDefinition condition, string ruleRecommendedAction) =>
        string.IsNullOrWhiteSpace(ruleRecommendedAction)
            ? condition.RecommendedAction
            : ruleRecommendedAction;
}

public static class PowerShellScriptValidator
{
    public static IReadOnlyList<string> GetParseErrors(string script)
    {
        Parser.ParseInput(script ?? string.Empty, out _, out var parseErrors);

        return parseErrors
            .Select(error => $"{error.Extent.StartLineNumber}:{error.Extent.StartColumnNumber} {error.Message}")
            .ToList();
    }
}
