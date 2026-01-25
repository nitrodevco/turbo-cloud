using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Rooms.Snapshots.Wired.Variables;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Variables;
using Turbo.Rooms.Wired.Variables;

namespace Turbo.Rooms.Grains.Systems;

public sealed partial class RoomWiredSystem
{
    private readonly HashSet<int> _dirtyVariableBoxIds = [];
    private readonly Dictionary<int, WiredVariableId> _variableIdBoxId = [];
    private readonly Dictionary<WiredVariableId, IWiredVariable> _variableById = [];

    private WiredVariablesSnapshot? _variablesSnapshot = null;

    public IWiredVariable? GetVariableById(WiredVariableId id)
    {
        if (_variableById.TryGetValue(id, out var variable))
            return variable;

        return null;
    }

    public Task<WiredVariablesSnapshot> GetWiredVariablesSnapshotAsync(CancellationToken ct) =>
        Task.FromResult(_variablesSnapshot ??= BuildVariablesSnapshot());

    public Task<List<(WiredVariableId id, int value)>> GetAllVariablesForBindingAsync(
        WiredVariableBinding binding,
        CancellationToken ct
    )
    {
        var variableValues = new List<(WiredVariableId id, int value)>();

        foreach (var variable in _variableById.Values)
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
            ProcessVariable(variable);

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
            !_roomGrain._state.ItemsById.TryGetValue(boxId, out var item)
            || item.Logic is not FurnitureWiredVariableLogic variable
        )
            return;

        await variable.LoadWiredAsync(ct);

        if (!ProcessVariable(variable))
            return;

        var snapshot = variable.GetVarSnapshot();

        _variableIdBoxId[boxId] = snapshot.VariableId;
    }

    private bool ProcessVariable(IWiredVariable variable)
    {
        var key = variable.GetVariableKey();

        if (string.IsNullOrWhiteSpace(key.VariableName))
            return false;

        var snapshot = variable.GetVarSnapshot();

        _variableById[snapshot.VariableId] = variable;

        return true;
    }

    private void RemoveVariableBox(int boxId)
    {
        if (!_variableIdBoxId.TryGetValue(boxId, out var variableId))
            return;

        _variableIdBoxId.Remove(boxId);
        _variableById.Remove(variableId);
    }

    private WiredVariablesSnapshot BuildVariablesSnapshot()
    {
        var hashes = new List<WiredVariableHash>();
        var snapshots = new List<WiredVariableSnapshot>(_variableById.Count);

        foreach (var variable in _variableById.Values)
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
