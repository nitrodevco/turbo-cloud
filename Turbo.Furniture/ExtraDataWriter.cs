using System.Text.Json.Nodes;
using Turbo.Primitives.Furniture.ExtraData;

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
        _root[name] = FurnitureExtraDataSections.ToNode(section);

    public void RemoveSection(string name) => _root.Remove(name);

    public string ToJsonString() => _root.ToJsonString();
}
