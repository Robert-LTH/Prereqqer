using System.Diagnostics;
using Prereqqer.Core.Definitions;

namespace Prereqqer.Core.Execution;

public sealed class ConditionRunService
{
    private readonly IConditionExecutor _conditionExecutor;

    public ConditionRunService(IConditionExecutor? conditionExecutor = null)
    {
        _conditionExecutor = conditionExecutor ?? new ConditionExecutor();
    }

    public async Task<RunExecutionResult> RunAsync(
        ConditionDefinitionDocument document,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var groups = new List<GroupExecutionResult>();

        foreach (var group in document.Groups)
        {
            cancellationToken.ThrowIfCancellationRequested();
            groups.Add(await RunGroupAsync(group, cancellationToken).ConfigureAwait(false));
        }

        stopwatch.Stop();

        return new RunExecutionResult
        {
            Groups = groups,
            Duration = stopwatch.Elapsed,
            Outcome = Aggregate(groups.Select(group => group.Outcome))
        };
    }

    public async Task<GroupExecutionResult> RunGroupAsync(
        ConditionGroupDefinition group,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var results = new List<ConditionExecutionResult>();

        if (!group.Enabled)
        {
            results.Add(new ConditionExecutionResult
            {
                ConditionId = group.Id,
                Outcome = CheckOutcome.PassedWithWarning,
                Message = "Group is disabled; no conditions were run.",
                Duration = TimeSpan.Zero
            });
        }
        else
        {
            foreach (var condition in group.Conditions)
            {
                cancellationToken.ThrowIfCancellationRequested();
                results.Add(await _conditionExecutor.ExecuteAsync(condition, cancellationToken).ConfigureAwait(false));
            }
        }

        stopwatch.Stop();

        return new GroupExecutionResult
        {
            GroupId = group.Id,
            Conditions = results,
            Duration = stopwatch.Elapsed,
            Outcome = Aggregate(results.Select(result => result.Outcome))
        };
    }

    private static CheckOutcome Aggregate(IEnumerable<CheckOutcome> outcomes)
    {
        var values = outcomes.ToList();
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
}
