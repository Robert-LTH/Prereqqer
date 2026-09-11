using System.Diagnostics;
using Prereqqer.Core.Definitions;

namespace Prereqqer.Core.Execution;

public sealed class ConditionExecutor : IConditionExecutor
{
    private readonly PowerShellConditionExecutor _powerShellConditionExecutor;
    private readonly WmiConditionExecutor _wmiConditionExecutor;

    public ConditionExecutor()
        : this(new PowerShellConditionExecutor(), new WmiConditionExecutor())
    {
    }

    public ConditionExecutor(
        PowerShellConditionExecutor powerShellConditionExecutor,
        WmiConditionExecutor wmiConditionExecutor)
    {
        _powerShellConditionExecutor = powerShellConditionExecutor;
        _wmiConditionExecutor = wmiConditionExecutor;
    }

    public Task<ConditionExecutionResult> ExecuteAsync(
        ConditionDefinition condition,
        CancellationToken cancellationToken = default)
    {
        if (!condition.Enabled)
        {
            return Task.FromResult(new ConditionExecutionResult
            {
                ConditionId = condition.Id,
                Outcome = CheckOutcome.PassedWithWarning,
                Message = "Condition is disabled; skipped.",
                Duration = TimeSpan.Zero,
                RecommendedAction = condition.RecommendedAction
            });
        }

        var timeout = TimeSpan.FromSeconds(Math.Clamp(condition.TimeoutSeconds, 1, 3600));

        return condition.Type switch
        {
            ConditionType.PowerShell => _powerShellConditionExecutor.ExecuteAsync(condition, timeout, cancellationToken),
            ConditionType.Wmi => _wmiConditionExecutor.ExecuteAsync(condition, timeout, cancellationToken),
            _ => Task.FromResult(ConditionExecutionResult.ExecutionError(
                condition.Id,
                "Unsupported condition type.",
                Stopwatch.GetElapsedTime(Stopwatch.GetTimestamp()),
                condition.RecommendedAction))
        };
    }
}
