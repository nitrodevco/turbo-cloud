using System;
using System.Text.Json;
using System.Threading.Tasks;
using Turbo.Primitives.Furniture;

namespace Turbo.Furniture;

/// <summary>
/// A furni's extra data: named JSON sections kept as one tree. A change edits the tree only; the
/// JSON text, and the parsed copy sections are read from, are rebuilt the next time something
/// asks for them. A dice or a lamp changes state many times between two writes to the database,
/// and every change used to write the whole document out and parse it back in.
/// </summary>
public sealed class ExtraData(string? extraData) : IExtraData
{
    private readonly ExtraDataWriter _writer = new(extraData);

    // Null when the tree has changed since they were last built.
    private ExtraDataReader? _reader = new(extraData);
    private string? _snapshot = extraData ?? "{}";

    private Func<Task>? _onSnapshotChanged;

    public void SetAction(Func<Task>? onSnapshotChanged) => _onSnapshotChanged = onSnapshotChanged;

    public bool TryGetSection(string name, out JsonElement element) =>
        (_reader ??= new ExtraDataReader(GetJsonString())).TryGet(name, out element);

    public void UpdateSection<TSection>(string name, TSection section)
    {
        _writer.SetSection(name, section);

        Changed();
    }

    public void DeleteSection(string name)
    {
        _writer.RemoveSection(name);

        Changed();
    }

    public string GetJsonString() => _snapshot ??= _writer.ToJsonString();

    private void Changed()
    {
        _snapshot = null;
        _reader = null;

        _ = _onSnapshotChanged?.Invoke();
    }
}
