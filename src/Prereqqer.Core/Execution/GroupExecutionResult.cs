using Prereqqer.Core.Definitions;

namespace Prereqqer.Core.Execution;

public sealed class GroupExecutionResult
{
    public string GroupId { get; init; } = string.Empty;

    public CheckOutcome Outcome { get; init; }

    public TimeSpan Duration { get; init; }

    public List<ConditionExecutionResult> Conditions { get; init; } = [];
}
