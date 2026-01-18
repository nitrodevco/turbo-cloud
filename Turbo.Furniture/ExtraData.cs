using System;
using System.Text.Json;
using System.Threading.Tasks;
using Turbo.Primitives.Furniture;

namespace Turbo.Furniture;

public sealed class ExtraData(string? extraData) : IExtraData
{
    private ExtraDataReader _reader = new(extraData);
    private readonly ExtraDataWriter _writer = new(extraData);

    private string _snapshot = extraData ?? "{}";
    private Func<Task>? _onSnapshotChanged;

    public void SetAction(Func<Task>? onSnapshotChanged) => _onSnapshotChanged = onSnapshotChanged;

    public bool TryGetSection(string name, out JsonElement element) =>
        _reader.TryGet(name, out element);

    public void UpdateSection<TSection>(string name, TSection section)
    {
        var updated = _writer.UpdateSection(name, section);

        _snapshot = updated;
        _reader = new ExtraDataReader(updated);

        _ = _onSnapshotChanged?.Invoke();
    }

    public void DeleteSection(string name)
    {
        var updated = _writer.DeleteSection(name);

        _snapshot = updated;
        _reader = new ExtraDataReader(updated);

        _ = _onSnapshotChanged?.Invoke();
    }

    public string GetJsonString() => _snapshot;
}
