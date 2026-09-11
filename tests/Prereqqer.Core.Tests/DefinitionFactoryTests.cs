using Prereqqer.Core.Definitions;
using Prereqqer.Core.Execution;
using Prereqqer.Core.Validation;

namespace Prereqqer.Core.Tests;

public sealed class DefinitionFactoryTests
{
    [Fact]
    public void CreateDefaultDocument_includes_server_api_and_service_checks()
    {
        var document = DefinitionFactory.CreateDefaultDocument();
        var conditionNames = document.Groups
            .SelectMany(group => group.Conditions)
            .Select(condition => condition.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains("Windows Server version", conditionNames);
        Assert.Contains("SQL Server version", conditionNames);
        Assert.Contains("DNS status", conditionNames);
        Assert.Contains("IIS installation and function", conditionNames);
        Assert.Contains("API JSON property", conditionNames);
    }

    [Fact]
    public void CreateDefaultDocument_includes_standard_file_checks_except_local_machine()
    {
        var expectedConditionNames = new[]
        {
            "macOS version",
            "CPU architecture",
            "Free disk space",
            "Memory pressure",
            "Power source",
            "PowerShell installed",
            ".NET SDK",
            "Node.js",
            "Python 3",
            "Homebrew",
            "Xcode Command Line Tools",
            "Rosetta 2",
            "Gatekeeper",
            "System Integrity Protection",
            "FileVault",
            "Application firewall",
            "DNS resolution",
            "Internet HTTPS access",
            "VPN interface",
            "GitHub reachable",
            "NuGet reachable",
            "npm registry reachable",
            "Git installed",
            "Git identity",
            "SSH agent",
            "SSH keys",
            "Docker installed",
            "Docker daemon",
            "Kubernetes context"
        };
        var document = DefinitionFactory.CreateDefaultDocument();
        var defaultConditionNames = document.Groups
            .Where(group => !string.Equals(group.Name, "Local machine", StringComparison.OrdinalIgnoreCase))
            .SelectMany(group => group.Conditions)
            .Select(condition => condition.Name)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var expectedConditionName in expectedConditionNames)
        {
            Assert.Contains(expectedConditionName, defaultConditionNames);
        }
    }

    [Fact]
    public void Built_in_power_shell_checks_are_valid_definitions()
    {
        var validator = new DefinitionValidator();
        var conditions = DefinitionFactory.CreateDefaultDocument()
            .Groups
            .SelectMany(group => group.Conditions)
            .Where(condition => condition.Type == ConditionType.PowerShell);

        foreach (var condition in conditions)
        {
            var result = validator.ValidateCondition(condition);

            Assert.Empty(result.Errors);
            Assert.Empty(PowerShellScriptValidator.GetParseErrors(condition.PowerShell.Script));
            Assert.NotEmpty(condition.Rules);
        }
    }

    [Fact]
    public void Api_json_property_check_verifies_matches_field()
    {
        var condition = DefinitionFactory.CreateApiJsonPropertyCondition();

        Assert.Contains("Invoke-RestMethod", condition.PowerShell.Script, StringComparison.Ordinal);
        Assert.Contains("PropertyPath", condition.PowerShell.Script, StringComparison.Ordinal);
        Assert.Contains(condition.Rules, rule =>
            rule.Outcome == CheckOutcome.Error
            && rule.Field == "Matches"
            && rule.Operator == RuleOperator.NotEquals
            && rule.ExpectedValue == "True");
        Assert.Contains(condition.Rules, rule =>
            rule.Outcome == CheckOutcome.Passed
            && rule.Field == "Matches"
            && rule.Operator == RuleOperator.Equals
            && rule.ExpectedValue == "True");
    }
}
