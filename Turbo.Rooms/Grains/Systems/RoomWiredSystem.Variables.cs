using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Furniture;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Snapshots.Wired.Variables;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Variables;
using Turbo.Rooms.Wired;
using Turbo.Rooms.Wired.Storage;
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

    /// <summary>The hash of every variable as last sent; a box editor echoes it to ask what changed.</summary>
    public WiredVariableHash AllVariablesHash { get; private set; } = new(0);

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

    public Task<
        List<(WiredVariableId id, WiredVariableValue value)>
    > GetAllVariablesForBindingAsync(WiredVariableBinding binding, CancellationToken ct)
    {
        var variableValues = new List<(WiredVariableId id, WiredVariableValue value)>();
        var targetId = binding.TargetId;

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
                foreach (var item in _roomGrain.FurniModule.Items)
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
                foreach (var avatar in _roomGrain.AvatarModule.Avatars)
                {
                    var key = new WiredVariableKey(
                        variableId,
                        WiredVariableTargetType.User,
                        avatar.ObjectId
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

    /// <summary>
    /// The wired menu's inspection tab edits, gives or takes away a variable on one furni or
    /// user. The flags asked for are the ones the client checks before it offers each button;
    /// the client is not trusted to have checked them.
    /// </summary>
    public async Task<bool> ApplyVariableMenuOperationAsync(
        WiredVariableBinding binding,
        WiredVariableId variableId,
        WiredVariableMenuOperationType operation,
        WiredVariableValue value,
        CancellationToken ct
    )
    {
        var variable = GetVariableById(variableId);

        // Only on a furni or user that is in the room right now: the target id comes from the
        // client, and an unchecked one would let values pile up under ids that belong to
        // nothing here.
        if (variable is null || !IsLiveTarget(binding))
            return false;

        var flags = variable.GetVarSnapshot().Flags;
        var key = new WiredVariableKey(variableId, binding.TargetType, binding.TargetId);

        switch (operation)
        {
            case WiredVariableMenuOperationType.SetValue:
                if (
                    !flags.Has(WiredVariableFlags.HasValue)
                    || !flags.Has(WiredVariableFlags.CanWriteValue)
                )
                    return false;

                return await variable.SetValueAsync(
                    new WiredExecutionContext(_roomGrain) { CancellationToken = ct },
                    key,
                    value
                );
            case WiredVariableMenuOperationType.Create:
                if (!flags.Has(WiredVariableFlags.CanCreateAndDelete))
                    return false;

                // A variable without a value is only held or not; whatever number came along
                // is not kept.
                return await variable.GiveValueAsync(
                    key,
                    flags.Has(WiredVariableFlags.HasValue) ? value : WiredVariableValue.Default
                );
            case WiredVariableMenuOperationType.Delete:
                return flags.Has(WiredVariableFlags.CanCreateAndDelete)
                    && variable.RemoveValue(key);
            default:
                return false;
        }
    }

    /// <summary>
    /// The overview tab's "delete" on a variable: it is taken from everyone and everything that
    /// holds it, whether or not they are in the room. The client offers this for a stored furni
    /// or user variable only, and that is all a box can do it for: only a stored variable keeps
    /// its own list of holders.
    /// </summary>
    public int RemoveVariableFromAllHolders(WiredVariableId variableId) =>
        GetVariableById(variableId) is FurnitureWiredVariableLogic box
        && box.GetVarSnapshot().Flags.Has(WiredVariableFlags.CanCreateAndDelete)
        && box.GetVarSnapshot().TargetType
            is WiredVariableTargetType.Furni
                or WiredVariableTargetType.User
            ? box.RemoveAllValues()
            : 0;

    /// <summary>
    /// A stored furni variable outlives the furni that holds it, which is right for a furni
    /// somebody owns and wrong for one nobody does. A temporary furni's id is handed out again
    /// the next time the room loads, and a borrowed one's as soon as the room needs another, so
    /// in both cases the next furni to get that id would inherit these values.
    /// </summary>
    private void ForgetStoredValuesOfUnownedFurni(RoomObjectId objectId)
    {
        if (!FurniIdBands.IsBuildersClub(objectId.Value) && objectId.Value >= 0)
            return;

        foreach (var variable in _variableById.Values)
        {
            var snapshot = variable.GetVarSnapshot();

            if (snapshot.TargetType == WiredVariableTargetType.Furni)
                variable.RemoveValue(
                    new WiredVariableKey(
                        snapshot.VariableId,
                        WiredVariableTargetType.Furni,
                        objectId.Value
                    )
                );
        }
    }

    private bool IsLiveTarget(WiredVariableBinding binding) =>
        binding.TargetType switch
        {
            WiredVariableTargetType.User => _roomGrain.AvatarModule.TryGetAvatar(
                binding.TargetId,
                out var avatar
            )
                && avatar is IRoomPlayer,
            WiredVariableTargetType.Furni => _roomGrain.FurniModule.HasItem(binding.TargetId),
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

        if (!_roomGrain.FurniModule.TryGetItem(boxId, out var item))
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

        AllVariablesHash = allVariablesSnapshot.AllVariablesHash;

        return allVariablesSnapshot;
    }
}
