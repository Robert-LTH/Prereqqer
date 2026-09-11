namespace Prereqqer.Core.Definitions;

public sealed class ConditionDefinitionDocument
{
    public int SchemaVersion { get; set; } = 1;

    public BrandingDefinition Branding { get; set; } = new();

    public ConfigurationSourceDefinition ConfigurationSource { get; set; } = new();

    public List<ConditionGroupDefinition> Groups { get; set; } = [];
}

public sealed class ConfigurationSourceDefinition
{
    public string Url { get; set; } = string.Empty;

    public bool FetchOnStartup { get; set; }

    public int TimeoutSeconds { get; set; } = 20;
}

public sealed class BrandingDefinition
{
    public string ApplicationName { get; set; } = "Prereqqer";

    public string Subtitle { get; set; } = "Prerequisite checks";

    public string HeaderBackgroundColor { get; set; } = "#172033";

    public string HeaderForegroundColor { get; set; } = "#FFFFFF";

    public string AccentColor { get; set; } = "#075EAD";
}

public sealed class ConditionGroupDefinition
{
    public string Id { get; set; } = IdFactory.NewId("grp");

    public string Name { get; set; } = "New group";

    public string Description { get; set; } = string.Empty;

    public bool Enabled { get; set; } = true;

    public List<ConditionDefinition> Conditions { get; set; } = [];
}

public sealed class ConditionDefinition
{
    public string Id { get; set; } = IdFactory.NewId("cond");

    public int Version { get; set; } = 1;

    public string Name { get; set; } = "New condition";

    public string Description { get; set; } = string.Empty;

    public string RecommendedAction { get; set; } = string.Empty;

    public bool RequiresElevation { get; set; }

    public bool Enabled { get; set; } = true;

    public bool RunInParallel { get; set; }

    public ConditionType Type { get; set; } = ConditionType.PowerShell;

    public int TimeoutSeconds { get; set; } = 30;

    public WmiCheckDefinition Wmi { get; set; } = new();

    public PowerShellCheckDefinition PowerShell { get; set; } = new();

    public List<OutcomeRuleDefinition> Rules { get; set; } = [];
}

public sealed class WmiCheckDefinition
{
    public string Namespace { get; set; } = @"root\CIMV2";

    public string Query { get; set; } = "SELECT Caption, Version FROM Win32_OperatingSystem";
}

public sealed class PowerShellCheckDefinition
{
    public string Script { get; set; } =
        """
        [pscustomobject]@{
            Major = $PSVersionTable.PSVersion.Major
            Platform = if ($IsWindows) { "Windows" } elseif ($IsMacOS) { "macOS" } elseif ($IsLinux) { "Linux" } else { "Unknown" }
        }
        """;
}

public sealed class OutcomeRuleDefinition
{
    public string Id { get; set; } = IdFactory.NewId("rule");

    public string Name { get; set; } = "New rule";

    public CheckOutcome Outcome { get; set; } = CheckOutcome.Passed;

    public RuleTarget Target { get; set; } = RuleTarget.FirstRow;

    public string Field { get; set; } = "Value";

    public RuleOperator Operator { get; set; } = RuleOperator.Equals;

    public string ExpectedValue { get; set; } = string.Empty;

    public string RecommendedAction { get; set; } = string.Empty;
}

public enum ConditionType
{
    Wmi,
    PowerShell
}

public enum CheckOutcome
{
    Passed,
    PassedWithWarning,
    Warning,
    Error,
    Cancelled
}

public enum RuleTarget
{
    RowCount,
    FirstRow,
    AnyRow,
    AllRows,
    Scalar
}

public enum RuleOperator
{
    Exists,
    NotExists,
    Equals,
    NotEquals,
    GreaterThan,
    GreaterThanOrEqual,
    LessThan,
    LessThanOrEqual,
    Contains,
    NotContains,
    RegexMatch,
    RegexNotMatch
}

public static class IdFactory
{
    public static string NewId(string prefix) => $"{prefix}_{Guid.NewGuid():N}";
}
