using Prereqqer.Core.Definitions;

namespace Prereqqer.Core.Execution;

public sealed class RunExecutionResult
{
    public CheckOutcome Outcome { get; init; }

    public TimeSpan Duration { get; init; }

    public List<GroupExecutionResult> Groups { get; init; } = [];

    public bool IsReady => !Groups
        .SelectMany(group => group.Conditions)
        .Any(condition => condition.Outcome == CheckOutcome.Error);
}
