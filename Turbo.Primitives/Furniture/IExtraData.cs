using System.Text.Json;

namespace Turbo.Primitives.Furniture;

public interface IExtraData
{
    /// <summary>What to do each time the data changes (the room marks the item to be saved).</summary>
    public void SetAction(System.Action? onSnapshotChanged);
    public bool TryGetSection(string name, out JsonElement element);
    public void UpdateSection<TSection>(string name, TSection section);
    public void DeleteSection(string name);
    public string GetJsonString();
}
