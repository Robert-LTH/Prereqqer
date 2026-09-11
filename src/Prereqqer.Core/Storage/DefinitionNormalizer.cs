using Prereqqer.Core.Definitions;

namespace Prereqqer.Core.Storage;

public static class DefinitionNormalizer
{
    public static ConditionDefinitionDocument Normalize(ConditionDefinitionDocument document)
    {
        document.SchemaVersion = document.SchemaVersion <= 0 ? 1 : document.SchemaVersion;
        document.Branding ??= new BrandingDefinition();
        NormalizeBranding(document.Branding);
        document.ConfigurationSource ??= new ConfigurationSourceDefinition();
        NormalizeConfigurationSource(document.ConfigurationSource);
        document.Groups ??= [];

        foreach (var group in document.Groups)
        {
            if (string.IsNullOrWhiteSpace(group.Id))
            {
                group.Id = IdFactory.NewId("grp");
            }

            if (string.IsNullOrWhiteSpace(group.Name))
            {
                group.Name = "Unnamed group";
            }

            group.Description ??= string.Empty;
            group.Conditions ??= [];

            foreach (var condition in group.Conditions)
            {
                NormalizeCondition(condition);
            }
        }

        return document;
    }

    private static void NormalizeConfigurationSource(ConfigurationSourceDefinition source)
    {
        source.Url ??= string.Empty;
        source.TimeoutSeconds = Math.Clamp(source.TimeoutSeconds, 1, 300);
    }

    private static void NormalizeBranding(BrandingDefinition branding)
    {
        var defaults = new BrandingDefinition();

        if (string.IsNullOrWhiteSpace(branding.ApplicationName))
        {
            branding.ApplicationName = defaults.ApplicationName;
        }

        branding.Subtitle ??= string.Empty;

        if (string.IsNullOrWhiteSpace(branding.HeaderBackgroundColor))
        {
            branding.HeaderBackgroundColor = defaults.HeaderBackgroundColor;
        }

        if (string.IsNullOrWhiteSpace(branding.HeaderForegroundColor))
        {
            branding.HeaderForegroundColor = defaults.HeaderForegroundColor;
        }

        if (string.IsNullOrWhiteSpace(branding.AccentColor))
        {
            branding.AccentColor = defaults.AccentColor;
        }
    }

    public static ConditionDefinition NormalizeCondition(ConditionDefinition condition)
    {
        if (string.IsNullOrWhiteSpace(condition.Id))
        {
            condition.Id = IdFactory.NewId("cond");
        }

        if (string.IsNullOrWhiteSpace(condition.Name))
        {
            condition.Name = "Unnamed condition";
        }

        condition.Version = Math.Max(1, condition.Version);
        condition.Description ??= string.Empty;
        condition.RecommendedAction ??= string.Empty;
        condition.TimeoutSeconds = Math.Clamp(condition.TimeoutSeconds, 1, 3600);
        condition.Wmi ??= new WmiCheckDefinition();
        condition.PowerShell ??= new PowerShellCheckDefinition();
        condition.Rules ??= [];

        foreach (var rule in condition.Rules)
        {
            if (string.IsNullOrWhiteSpace(rule.Id))
            {
                rule.Id = IdFactory.NewId("rule");
            }

            if (string.IsNullOrWhiteSpace(rule.Name))
            {
                rule.Name = "Unnamed rule";
            }

            rule.Field ??= string.Empty;
            rule.ExpectedValue ??= string.Empty;
            rule.RecommendedAction ??= string.Empty;
        }

        return condition;
    }
}
