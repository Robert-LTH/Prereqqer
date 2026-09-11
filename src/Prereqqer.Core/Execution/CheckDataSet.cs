using System.Text.Json;

namespace Prereqqer.Core.Execution;

public sealed class CheckDataSet
{
    public List<Dictionary<string, object?>> Rows { get; set; } = [];

    public Dictionary<string, object?> Scalars { get; set; } = [];

    public int RowCount => Rows.Count;

    public string ToJson()
    {
        var payload = new
        {
            rowCount = RowCount,
            rows = Rows,
            scalars = Scalars
        };

        return JsonSerializer.Serialize(payload, SerializerOptions);
    }

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };
}
