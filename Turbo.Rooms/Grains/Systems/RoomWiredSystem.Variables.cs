using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Rooms.Snapshots.Wired.Variables;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Variables;
using Turbo.Rooms.Wired;
using Turbo.Rooms.Wired.Variables;

namespace Turbo.Rooms.Grains.Systems;

public sealed partial class RoomWiredSystem
{
    private readonly HashSet<int> _dirtyVariableBoxIds = [];
    private readonly Dictionary<int, WiredVariableKey> _variableKeyBoxId = [];
    private readonly Dictionary<WiredVariableKey, IWiredVariable> _variableByKey = [];

    private WiredVariablesSnapshot? _variablesSnapshot = null;

    public bool TryGetVariable(
        string key,
        in WiredVariableBinding binding,
        WiredExecutionContext ctx,
        out int value
    )
    {
        var variableKey = new WiredVariableKey(binding.TargetType, key);

        if (_variableByKey.TryGetValue(variableKey, out var variable))
            return variable.TryGet(binding, out value);

        value = 0;

        return false;
    }

    public Task<WiredVariablesSnapshot> GetWiredVariablesSnapshotAsync(CancellationToken ct) =>
        Task.FromResult(_variablesSnapshot ??= BuildVariablesSnapshot());

    public Task<List<(WiredVariableId id, int value)>> GetAllVariablesForBindingAsync(
        WiredVariableBinding binding,
        CancellationToken ct
    )
    {
        var variableValues = new List<(WiredVariableId id, int value)>();

        foreach (var variable in _variableByKey.Values)
        {
            if (!variable.TryGet(binding, out var value))
                continue;

            var snapshot = variable.GetVarSnapshot();

            variableValues.Add((snapshot.VariableId, value));
        }

        return Task.FromResult(variableValues);
    }

    private Task ProcessInternalVariablesAsync(long now, CancellationToken ct)
    {
        var variables = _roomGrain._wiredVariablesProvider.BuildVariablesForRoom(_roomGrain);

        foreach (var variable in variables)
        {
            var key = variable.GetVariableKey();

            if (string.IsNullOrWhiteSpace(key.VariableName))
                continue;

            _variableByKey[key] = variable;
        }

        return Task.CompletedTask;
    }

    private async Task ProcessVariableBoxesAsync(long now, CancellationToken ct)
    {
        if (_dirtyVariableBoxIds.Count == 0)
            return;

        var dirtyVariableBoxIds = _dirtyVariableBoxIds.ToList();
        _dirtyVariableBoxIds.Clear();

        foreach (var boxId in dirtyVariableBoxIds)
            await ProcessVariableBoxAsync(boxId, ct);

        _variablesSnapshot = null;
    }

    private async Task ProcessVariableBoxAsync(int boxId, CancellationToken ct)
    {
        RemoveVariableBox(boxId);

        if (
            !_roomGrain._state.FloorItemsById.TryGetValue(boxId, out var floorItem)
            || floorItem.Logic is not FurnitureWiredVariableLogic variable
        )
            return;

        await variable.LoadWiredAsync(ct);

        var key = variable.GetVariableKey();

        if (string.IsNullOrWhiteSpace(key.VariableName))
            return;

        _variableByKey[key] = variable;
        _variableKeyBoxId[boxId] = key;
    }

    private void RemoveVariableBox(int boxId)
    {
        if (!_variableKeyBoxId.TryGetValue(boxId, out var key))
            return;

        _variableByKey.Remove(key);
        _variableKeyBoxId.Remove(boxId);
    }

    private WiredVariablesSnapshot BuildVariablesSnapshot()
    {
        var hashes = new List<WiredVariableHash>();
        var snapshots = new List<WiredVariableSnapshot>(_variableByKey.Count);

        foreach (var variable in _variableByKey.Values)
        {
            var snapshot = variable.GetVarSnapshot();

            hashes.Add(snapshot.VariableHash);
            snapshots.Add(snapshot);
        }

        var allVariablesSnapshot = new WiredVariablesSnapshot()
        {
            AllVariablesHash = WiredVariableHashBuilder.HashFromHashes(hashes),
            Variables = snapshots,
        };

        _roomGrain._state.AllVariablesHash = allVariablesSnapshot.AllVariablesHash;

        return allVariablesSnapshot;
    }
}
