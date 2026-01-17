using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Rooms.Snapshots.Wired;
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
        in IWiredVariableBinding binding,
        WiredExecutionContext ctx,
        out int value
    )
    {
        var variableKey = new WiredVariableKey(binding.Target, key);

        if (_variableByKey.TryGetValue(variableKey, out var variable))
            return variable.TryGet(binding, ctx, out value);

        value = 0;

        return false;
    }

    public Task<WiredVariablesSnapshot> GetWiredVariablesSnapshotAsync(CancellationToken ct) =>
        Task.FromResult(_variablesSnapshot ??= BuildVariablesSnapshot());

    private async Task ProcessInternalVariablesAsync(long now, CancellationToken ct)
    {
        var variables = _roomGrain._wiredVariablesProvider.BuildVariablesForRoom(_roomGrain);

        foreach (var variable in variables)
            _variableByKey.Add(
                new WiredVariableKey(
                    variable.VarDefinition.TargetType,
                    variable.VarDefinition.Name
                ),
                variable
            );
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
            || floorItem.Logic is not FurnitureWiredVariableLogic varLogic
        )
            return;

        await varLogic.LoadWiredAsync(ct);

        var defKey = varLogic.VarDefinition.Name;

        if (string.IsNullOrWhiteSpace(defKey))
            return;

        var key = new WiredVariableKey(varLogic.GetVariableTargetType(), defKey);

        _variableByKey[key] = varLogic;
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
        var hash = (long)0;
        var snapshots = new List<WiredVariableSnapshot>(_variableByKey.Count);

        foreach (var variable in _variableByKey.Values)
        {
            var snapshot = variable.VarDefinition.GetSnapshot();

            hash ^= snapshot.VariableHash;

            snapshots.Add(snapshot);
        }

        var allVariablesSnapshot = new WiredVariablesSnapshot()
        {
            AllVariablesHash = hash,
            Variables = snapshots,
        };

        _roomGrain._state.AllVariablesHash = hash;

        return allVariablesSnapshot;
    }
}
