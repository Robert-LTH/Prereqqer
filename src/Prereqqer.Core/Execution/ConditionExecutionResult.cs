using Prereqqer.Core.Definitions;

namespace Prereqqer.Core.Execution;

public sealed class ConditionExecutionResult
{
    public string ConditionId { get; init; } = string.Empty;

    public CheckOutcome Outcome { get; init; }

    public string Message { get; init; } = string.Empty;

    public string? MatchedRuleName { get; init; }

    public string RecommendedAction { get; init; } = string.Empty;

    public TimeSpan Duration { get; init; }

    public CheckDataSet Data { get; init; } = new();

    public List<string> Output { get; init; } = [];

    public List<string> Warnings { get; init; } = [];

    public List<string> Errors { get; init; } = [];

    public string RawData => Data.ToJson();

    public static ConditionExecutionResult ExecutionError(
        string conditionId,
        string message,
        TimeSpan duration,
        string recommendedAction = "",
        IEnumerable<string>? errors = null) =>
        new()
        {
            ConditionId = conditionId,
            Outcome = CheckOutcome.Error,
            Message = message,
            Duration = duration,
            RecommendedAction = recommendedAction,
            Errors = errors?.ToList() ?? [message]
        };
}
