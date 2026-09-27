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

    public void SetSection<TSection>(string name, TSection section) =>
        _root[name] = JsonSerializer.SerializeToNode(section, OPTIONS);

    public void RemoveSection(string name) => _root.Remove(name);

    public string ToJsonString() => _root.ToJsonString(OPTIONS);

    private static readonly JsonSerializerOptions OPTIONS = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };
}
