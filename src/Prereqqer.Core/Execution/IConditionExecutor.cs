using Prereqqer.Core.Definitions;

namespace Prereqqer.Core.Execution;

public interface IConditionExecutor
{
    Task<ConditionExecutionResult> ExecuteAsync(ConditionDefinition condition, CancellationToken cancellationToken = default);
}
