using System.Diagnostics;
using System.Management;
using System.Runtime.Versioning;
using Prereqqer.Core.Definitions;
using Prereqqer.Core.Rules;

namespace Prereqqer.Core.Execution;

public sealed class WmiConditionExecutor
{
    private readonly RuleEvaluator _ruleEvaluator;

    public WmiConditionExecutor(RuleEvaluator? ruleEvaluator = null)
    {
        _ruleEvaluator = ruleEvaluator ?? new RuleEvaluator();
    }

    public async Task<ConditionExecutionResult> ExecuteAsync(
        ConditionDefinition condition,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        var started = Stopwatch.GetTimestamp();

        if (!OperatingSystem.IsWindows())
        {
            return new ConditionExecutionResult
            {
                ConditionId = condition.Id,
                Outcome = CheckOutcome.Warning,
                Message = "WMI checks are only supported on Windows.",
                Duration = Stopwatch.GetElapsedTime(started),
                RecommendedAction = condition.RecommendedAction,
                Warnings = ["WMI is unavailable on this platform."]
            };
        }

        try
        {
#pragma warning disable CA1416
            var queryTask = Task.Run(() => ExecuteWindowsWmiQuery(condition, timeout), cancellationToken);
#pragma warning restore CA1416
            var delayTask = Task.Delay(timeout, cancellationToken);
            var completedTask = await Task.WhenAny(queryTask, delayTask).ConfigureAwait(false);

            if (completedTask != queryTask)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }

                return ConditionExecutionResult.ExecutionError(
                    condition.Id,
                    $"WMI query timed out after {timeout.TotalSeconds:0} seconds.",
                    Stopwatch.GetElapsedTime(started),
                    condition.RecommendedAction);
            }

            var data = await queryTask.ConfigureAwait(false);
            var evaluation = _ruleEvaluator.Evaluate(condition.Rules, data);

            return new ConditionExecutionResult
            {
                ConditionId = condition.Id,
                Outcome = evaluation.Outcome,
                Message = evaluation.Message,
                MatchedRuleName = evaluation.MatchedRuleName,
                RecommendedAction = ResolveRecommendedAction(condition, evaluation.RecommendedAction),
                Duration = Stopwatch.GetElapsedTime(started),
                Data = data
            };
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

    private static string ResolveRecommendedAction(ConditionDefinition condition, string ruleRecommendedAction) =>
        string.IsNullOrWhiteSpace(ruleRecommendedAction)
            ? condition.RecommendedAction
            : ruleRecommendedAction;

    [SupportedOSPlatform("windows")]
    private static CheckDataSet ExecuteWindowsWmiQuery(ConditionDefinition condition, TimeSpan timeout)
    {
        var scope = new ManagementScope(condition.Wmi.Namespace);
        var query = new ObjectQuery(condition.Wmi.Query);
        var options = new System.Management.EnumerationOptions
        {
            Timeout = timeout,
            ReturnImmediately = false
        };

        using var searcher = new ManagementObjectSearcher(scope, query, options);
        using var collection = searcher.Get();

        var data = new CheckDataSet();
        foreach (ManagementBaseObject item in collection)
        {
            var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (PropertyData property in item.Properties)
            {
                row[property.Name] = ValueNormalizer.Normalize(property.Value);
            }

            data.Rows.Add(row);
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
}
