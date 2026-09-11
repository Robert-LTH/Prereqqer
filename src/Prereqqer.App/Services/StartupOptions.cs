namespace Prereqqer.App.Services;

public static class StartupOptions
{
    public static bool IsDesignModeRequested { get; private set; }

    public static string ConfigurationUrl { get; private set; } = string.Empty;

    public static void Initialize(IEnumerable<string> args)
    {
        var values = args.ToArray();
        IsDesignModeRequested = values.Any(IsDesignModeArgument);
        ConfigurationUrl = ParseConfigurationUrl(values);
    }

    public static bool IsDesignModeArgument(string arg) =>
        string.Equals(arg, "--design", StringComparison.OrdinalIgnoreCase)
        || string.Equals(arg, "--design-mode", StringComparison.OrdinalIgnoreCase)
        || string.Equals(arg, "--admin", StringComparison.OrdinalIgnoreCase)
        || string.Equals(arg, "/design", StringComparison.OrdinalIgnoreCase)
        || string.Equals(arg, "/design-mode", StringComparison.OrdinalIgnoreCase)
        || string.Equals(arg, "/admin", StringComparison.OrdinalIgnoreCase);

    private static string ParseConfigurationUrl(IReadOnlyList<string> args)
    {
        for (var index = 0; index < args.Count; index++)
        {
            var arg = args[index];
            var inlineValue = TryReadInlineConfigurationUrl(arg);
            if (!string.IsNullOrWhiteSpace(inlineValue))
            {
                return inlineValue;
            }

            if (IsConfigurationUrlArgument(arg)
                && index + 1 < args.Count
                && !args[index + 1].StartsWith("-", StringComparison.Ordinal)
                && !args[index + 1].StartsWith("/", StringComparison.Ordinal))
            {
                return args[index + 1];
            }
        }

        return string.Empty;
    }

    private static string TryReadInlineConfigurationUrl(string arg)
    {
        foreach (var name in ConfigurationUrlArgumentNames)
        {
            var equalsPrefix = $"{name}=";
            if (arg.StartsWith(equalsPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return arg[equalsPrefix.Length..];
            }

            var colonPrefix = $"{name}:";
            if (arg.StartsWith(colonPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return arg[colonPrefix.Length..];
            }
        }

        return string.Empty;
    }

    private static bool IsConfigurationUrlArgument(string arg) =>
        ConfigurationUrlArgumentNames.Any(name => string.Equals(arg, name, StringComparison.OrdinalIgnoreCase));

    private static readonly string[] ConfigurationUrlArgumentNames =
    [
        "--config-url",
        "--configuration-url",
        "--definition-url",
        "--definitions-url",
        "/config-url",
        "/configuration-url",
        "/definition-url",
        "/definitions-url"
    ];
}
