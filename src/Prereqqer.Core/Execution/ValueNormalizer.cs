using System.Collections;
using System.Globalization;
using System.Management.Automation;

namespace Prereqqer.Core.Execution;

public static class ValueNormalizer
{
    public static Dictionary<string, object?> FromPowerShellObject(PSObject value)
    {
        var baseObject = value.BaseObject;

        if (IsScalar(baseObject))
        {
            return new Dictionary<string, object?>
            {
                ["Value"] = Normalize(baseObject)
            };
        }

        if (baseObject is IDictionary dictionary)
        {
            var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (DictionaryEntry entry in dictionary)
            {
                if (entry.Key is not null)
                {
                    row[Convert.ToString(entry.Key, CultureInfo.InvariantCulture) ?? string.Empty] = Normalize(entry.Value);
                }
            }

            return row;
        }

        var properties = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in value.Properties)
        {
            if (!property.IsGettable || string.IsNullOrWhiteSpace(property.Name))
            {
                continue;
            }

            try
            {
                properties[property.Name] = Normalize(property.Value);
            }
            catch (GetValueException)
            {
                properties[property.Name] = null;
            }
        }

        if (properties.Count == 0)
        {
            properties["Value"] = Normalize(baseObject?.ToString());
        }

        return properties;
    }

    public static object? Normalize(object? value)
    {
        if (value is null)
        {
            return null;
        }

        if (value is PSObject psObject)
        {
            return Normalize(psObject.BaseObject);
        }

        if (IsScalar(value))
        {
            return value switch
            {
                Enum enumValue => enumValue.ToString(),
                Version version => version.ToString(),
                DateTime dateTime => dateTime.ToString("O", CultureInfo.InvariantCulture),
                DateTimeOffset dateTimeOffset => dateTimeOffset.ToString("O", CultureInfo.InvariantCulture),
                TimeSpan timeSpan => timeSpan.ToString(),
                _ => value
            };
        }

        if (value is IEnumerable enumerable and not string)
        {
            var values = new List<object?>();
            foreach (var item in enumerable)
            {
                values.Add(Normalize(item));
            }

            return values;
        }

        return value.ToString();
    }

    public static string ToDisplayString(object? value) =>
        value switch
        {
            null => string.Empty,
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            IEnumerable enumerable and not string => string.Join(", ", enumerable.Cast<object?>().Select(ToDisplayString)),
            _ => value.ToString() ?? string.Empty
        };

    private static bool IsScalar(object? value) =>
        value is null
        || value is string
        || value is char
        || value is bool
        || value is byte
        || value is sbyte
        || value is short
        || value is ushort
        || value is int
        || value is uint
        || value is long
        || value is ulong
        || value is float
        || value is double
        || value is decimal
        || value is DateTime
        || value is DateTimeOffset
        || value is TimeSpan
        || value is Guid
        || value is Version
        || value is Enum;
}
