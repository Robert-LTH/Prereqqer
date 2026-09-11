using Prereqqer.Core.Definitions;

namespace Prereqqer.Core.Rules;

public sealed class RuleEvaluationResult
{
    public CheckOutcome Outcome { get; init; }

    public string Message { get; init; } = string.Empty;

    public string? MatchedRuleName { get; init; }

    public string RecommendedAction { get; init; } = string.Empty;

    public bool Matched => !string.IsNullOrWhiteSpace(MatchedRuleName);
}
