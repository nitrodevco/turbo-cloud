using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace Turbo.Primitives.Furniture;

public interface IExtraData
{
    public void SetAction(Func<Task>? onSnapshotChanged);
    public bool TryGetSection(string name, out JsonElement element);
    public void UpdateSection<TSection>(string name, TSection section);
    public void DeleteSection(string name);
    public string GetJsonString();
}
