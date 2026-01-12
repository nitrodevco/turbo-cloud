using System;
using Turbo.Primitives.Furniture;
using Turbo.Primitives.Rooms.Wired;

namespace Turbo.Rooms.Wired.Variables;

public sealed class WiredVariableRegistration : IWiredVariableRegistration
{
    public required IWiredVariableDefinition Definition { get; init; }
    public required IStorageData StorageData { get; init; }

    public bool TryGet(string key, out int value) => StorageData.TryGet(key, out value);

    public void SetValue(string key, int value) => StorageData.SetValue(key, value);

    public void RemoveValue(string key) => StorageData.Remove(key);
}
