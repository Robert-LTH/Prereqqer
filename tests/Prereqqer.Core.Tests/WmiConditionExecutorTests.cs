using Prereqqer.Core.Definitions;
using Prereqqer.Core.Execution;

namespace Prereqqer.Core.Tests;

public sealed class WmiConditionExecutorTests
{
    [Fact]
    public async Task ExecuteAsync_returns_warning_for_wmi_on_non_windows()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var condition = DefinitionFactory.CreateDefaultWmiCondition();

        var result = await new ConditionExecutor().ExecuteAsync(condition);

        Assert.Equal(CheckOutcome.Warning, result.Outcome);
        Assert.Contains("Windows", result.Message);
    }
}
