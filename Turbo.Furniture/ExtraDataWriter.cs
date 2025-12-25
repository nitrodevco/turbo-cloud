using System.Text.Json;
using System.Text.Json.Nodes;

namespace Turbo.Furniture;

internal sealed class ExtraDataWriter
{
    private readonly JsonObject _root;

    public ExtraDataWriter(string? extraData)
    {
        if (string.IsNullOrWhiteSpace(extraData))
        {
            _root = [];

            return;
        }

        _root = (JsonObject)JsonNode.Parse(extraData)!;
    }

    public string UpdateSection<TSection>(string name, TSection section)
    {
        _root[name] = JsonSerializer.SerializeToNode(section, OPTIONS);

        return _root.ToJsonString(OPTIONS);
    }

    public string ToJsonString() => _root.ToJsonString(OPTIONS);

    private static readonly JsonSerializerOptions OPTIONS = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };
}
