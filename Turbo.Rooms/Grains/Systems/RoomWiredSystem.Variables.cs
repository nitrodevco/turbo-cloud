using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Snapshots.Wired.Variables;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains.Storage;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Variables;
using Turbo.Rooms.Wired;
using Turbo.Rooms.Wired.Variables;

namespace Turbo.Rooms.Grains.Systems;

public sealed partial class RoomWiredSystem
{
    private readonly HashSet<int> _dirtyVariableBoxIds = [];
    private readonly Dictionary<int, WiredVariableId> _variableIdBoxId = [];
    private readonly Dictionary<WiredVariableId, IWiredVariable> _variableById = [];
    private readonly Dictionary<int, List<WiredVariableId>> _subVariableIdsByBoxId = [];
    private readonly FurnitureActiveStore _furnitureActiveStore = new();
    private readonly PlayerActiveStore _playerActiveStore = new();
    private readonly RoomActiveStore _roomActiveStore = new();
    private WiredVariablesSnapshot? _variablesSnapshot = null;

    public IWiredVariable? GetVariableById(WiredVariableId id)
    {
        if (_variableById.TryGetValue(id, out var variable))
            return variable;

        return null;
    }

    public IEnumerable<IWiredVariable> GetAllVariables() => _variableById.Values;

    public bool TryGetStoreForKey(WiredVariableKey key, out KeyValueStore? store)
    {
        store = null;

        return key.TargetType switch
        {
            WiredVariableTargetType.Furni => _furnitureActiveStore.TryGetStore(key, out store),
            WiredVariableTargetType.User => _playerActiveStore.TryGetStore(key, out store),
            WiredVariableTargetType.Global => _roomActiveStore.TryGetStore(key, out store),
            _ => false,
        };
    }

    public Task<WiredVariablesSnapshot> GetWiredVariablesSnapshotAsync(CancellationToken ct) =>
        Task.FromResult(_variablesSnapshot ??= BuildVariablesSnapshot());

    /// <summary>
    /// The client addresses users by room index; stores and selections use player ids. Maps a
    /// room index to the player id when one is in the room, otherwise passes the id through.
    /// </summary>
    public int ResolveTargetId(WiredVariableTargetType targetType, int targetId)
    {
        if (
            targetType == WiredVariableTargetType.User
            && _roomGrain._state.AvatarsByObjectId.TryGetValue(targetId, out var avatar)
            && avatar is IRoomPlayer player
        )
            return player.PlayerId;

        return targetId;
    }

    public Task<
        List<(WiredVariableId id, WiredVariableValue value)>
    > GetAllVariablesForBindingAsync(WiredVariableBinding binding, CancellationToken ct)
    {
        var variableValues = new List<(WiredVariableId id, WiredVariableValue value)>();
        var targetId = ResolveTargetId(binding.TargetType, binding.TargetId);

        foreach (var (id, variable) in _variableById)
        {
            var key = new WiredVariableKey(id, binding.TargetType, targetId);

            if (!variable.TryGetValue(key, out var value))
                continue;

            variableValues.Add((id, value));
        }

        return Task.FromResult(variableValues);
    }

    /// <summary>
    /// Every furni or user holding a value for a variable, as the wired menu lists them. Users
    /// are reported by room index. Null for an unknown variable.
    /// </summary>
    public WiredVariableInfoAndHoldersSnapshot? GetVariableHolders(WiredVariableId variableId)
    {
        var variable = GetVariableById(variableId);

        if (variable is null)
            return null;

        var snapshot = variable.GetVarSnapshot();
        var holders = new List<(int objectId, int value)>();

        switch (snapshot.TargetType)
        {
            case WiredVariableTargetType.Furni:
                foreach (var item in _roomGrain._state.ItemsById.Values)
                {
                    var key = new WiredVariableKey(
                        variableId,
                        WiredVariableTargetType.Furni,
                        item.ObjectId
                    );

                    if (variable.TryGetValue(key, out var value))
                        holders.Add((item.ObjectId, value));
                }
                break;
            case WiredVariableTargetType.User:
                foreach (var avatar in _roomGrain._state.AvatarsByObjectId.Values)
                {
                    if (avatar is not IRoomPlayer player)
                        continue;

                    var key = new WiredVariableKey(
                        variableId,
                        WiredVariableTargetType.User,
                        player.PlayerId
                    );

                    if (variable.TryGetValue(key, out var value))
                        holders.Add((avatar.ObjectId, value));
                }
                break;
            default:
            {
                var key = new WiredVariableKey(variableId, snapshot.TargetType, 0);

                if (variable.TryGetValue(key, out var value))
                    holders.Add((0, value));
                break;
            }
        }

        return new WiredVariableInfoAndHoldersSnapshot
        {
            ContextType = WiredContextType.VariableInfoAndValue,
            Variable = snapshot,
            Holders = holders,
        };
    }

    /// <summary>Writes a value from the wired menu; creates it when the variable allows it.</summary>
    public async Task<bool> SetVariableValueAsync(
        WiredVariableBinding binding,
        WiredVariableId variableId,
        WiredVariableValue value,
        CancellationToken ct
    )
    {
        var variable = GetVariableById(variableId);

        if (variable is null)
            return false;

        // The menu only edits values the variable itself lets wired write, and only on a furni
        // or user that is in the room right now: the target id comes from the client, and an
        // unchecked one would let values pile up under ids that belong to nothing here.
        if (
            !variable.GetVarSnapshot().Flags.Has(WiredVariableFlags.CanWriteValue)
            || !IsLiveTarget(binding)
        )
            return false;

        var key = new WiredVariableKey(
            variableId,
            binding.TargetType,
            ResolveTargetId(binding.TargetType, binding.TargetId)
        );

        if (variable.TryGetValue(key, out _))
        {
            var ctx = new WiredExecutionContext(_roomGrain) { CancellationToken = ct };

            return await variable.SetValueAsync(ctx, key, value);
        }

        return await variable.GiveValueAsync(key, value, true);
    }

    private bool IsLiveTarget(WiredVariableBinding binding) =>
        binding.TargetType switch
        {
            WiredVariableTargetType.User => _roomGrain._state.AvatarsByObjectId.TryGetValue(
                binding.TargetId,
                out var avatar
            )
                && avatar is IRoomPlayer,
            WiredVariableTargetType.Furni => _roomGrain._state.ItemsById.ContainsKey(
                binding.TargetId
            ),
            WiredVariableTargetType.Global or WiredVariableTargetType.Context => true,
            _ => false,
        };

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

        if (!_roomGrain._state.ItemsById.TryGetValue(boxId, out var item))
            return;

        switch (item.Logic)
        {
            case FurnitureWiredVariableLogic variable:
            {
                await variable.LoadWiredAsync(ct);

                if (!ProcessVariable(variable))
                    return;

                var snapshot = variable.GetVarSnapshot();

                _variableIdBoxId[boxId] = snapshot.VariableId;

                break;
            }
            case IWiredSubVariableProvider provider:
            {
                await provider.LoadWiredAsync(ct);

                var ids = new List<WiredVariableId>();

                foreach (var subVariable in provider.GetSubVariables())
                {
                    if (!ProcessVariable(subVariable))
                        continue;

                    ids.Add(subVariable.GetVarSnapshot().VariableId);
                }

                if (ids.Count > 0)
                    _subVariableIdsByBoxId[boxId] = ids;

                break;
            }
        }
    }

    private bool ProcessVariable(IWiredVariable variable)
    {
        var snapshot = variable.GetVarSnapshot();

        if (string.IsNullOrWhiteSpace(snapshot.VariableName))
            return false;

        _variableById[snapshot.VariableId] = variable;

        return true;
    }

    private void RemoveVariableBox(int boxId)
    {
        if (_variableIdBoxId.TryGetValue(boxId, out var variableId))
        {
            _variableIdBoxId.Remove(boxId);
            _variableById.Remove(variableId);
        }

        if (_subVariableIdsByBoxId.TryGetValue(boxId, out var subIds))
        {
            _subVariableIdsByBoxId.Remove(boxId);

            foreach (var subId in subIds)
                _variableById.Remove(subId);
        }
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
