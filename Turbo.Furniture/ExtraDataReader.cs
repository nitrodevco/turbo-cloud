using System.Text.Json;

namespace Turbo.Furniture;

internal sealed class ExtraDataReader
{
    private readonly JsonElement _root;

    public ExtraDataReader(string? extraData)
    {
        if (string.IsNullOrWhiteSpace(extraData))
        {
            _root = default;

            return;
        }

        _root = JsonDocument.Parse(extraData).RootElement;
    }

    public bool TryGet(string name, out JsonElement element)
    {
        if (_root.ValueKind != JsonValueKind.Object)
        {
            element = default;

            return false;
        }

        return _root.TryGetProperty(name, out element);
    }
}
