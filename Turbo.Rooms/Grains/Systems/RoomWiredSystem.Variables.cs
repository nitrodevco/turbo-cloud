using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Furniture;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Primitives.Rooms.Object;
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

    /// <summary>
    /// The context variable values of the wired execution running now (<see cref="Turbo.Rooms.Wired.WiredContext.ContextValues"/>):
    /// set while a stack's selectors, conditions and addons run and while each of its actions runs,
    /// null between them, so a context variable holds nothing outside a wired execution.
    /// </summary>
    private KeyValueStore? _contextValues;

    /// <summary>The context values a signal or stack call carries to the stacks it starts.</summary>
    private readonly System.Runtime.CompilerServices.ConditionalWeakTable<
        RoomEvent,
        KeyValueStore
    > _contextValuesByEvent = [];

    /// <summary>
    /// Lets the stacks a signal or a stack call starts begin from <paramref name="values"/>: each
    /// takes a copy, as Wired Faculty's "Memorization with Signals" describes.
    /// </summary>
    public void CarryContextValues(RoomEvent evt, KeyValueStore values) =>
        _contextValuesByEvent.AddOrUpdate(evt, values);

    /// <summary>A copy of what <paramref name="evt"/> carries, or an empty context.</summary>
    private KeyValueStore ContextValuesFor(RoomEvent evt) =>
        _contextValuesByEvent.TryGetValue(evt, out var values)
            ? values.Clone()
            : new KeyValueStore();

    public bool TryGetStoreForKey(WiredVariableKey key, out KeyValueStore? store)
    {
        store = null;

        return key.TargetType switch
        {
            WiredVariableTargetType.Furni => _furnitureActiveStore.TryGetStore(key, out store),
            WiredVariableTargetType.User => _playerActiveStore.TryGetStore(key, out store),
            WiredVariableTargetType.Global => _roomActiveStore.TryGetStore(key, out store),
            WiredVariableTargetType.Context => (store = _contextValues) is not null,
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

        foreach (var targetId in GetLiveTargetIds(snapshot.TargetType))
        {
            var key = new WiredVariableKey(variableId, snapshot.TargetType, targetId);

            if (variable.TryGetValue(key, out var value))
                holders.Add((targetId, value));
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
    /// <summary>
    /// Where the variable changes made right now come from: wired in this room, unless the
    /// inspection tool or Variable Management is making them (<see cref="WiredVariableChangedEvent.Origin"/>).
    /// </summary>
    public WiredVariableChangeOriginType ChangeOrigin { get; private set; }

    public async Task<bool> ApplyVariableMenuOperationAsync(
        WiredVariableBinding binding,
        WiredVariableId variableId,
        WiredVariableMenuOperationType operation,
        WiredVariableValue value,
        CancellationToken ct
    )
    {
        ChangeOrigin = WiredVariableChangeOriginType.Inspection;

        try
        {
            return await ApplyVariableMenuOperationCoreAsync(
                binding,
                variableId,
                operation,
                value,
                ct
            );
        }
        finally
        {
            ChangeOrigin = WiredVariableChangeOriginType.ThisRoom;
        }
    }

    private async Task<bool> ApplyVariableMenuOperationCoreAsync(
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
    public int RemoveVariableFromAllHolders(WiredVariableId variableId)
    {
        ChangeOrigin = WiredVariableChangeOriginType.External;

        try
        {
            return
                GetVariableById(variableId) is FurnitureWiredVariableLogic box
                && box.GetVarSnapshot().Flags.Has(WiredVariableFlags.CanCreateAndDelete)
                && box.GetVarSnapshot().TargetType
                    is WiredVariableTargetType.Furni
                        or WiredVariableTargetType.User
                ? box.RemoveAllValues()
                : 0;
        }
        finally
        {
            ChangeOrigin = WiredVariableChangeOriginType.ThisRoom;
        }
    }

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

    /// <summary>
    /// Everything in the room a variable of this kind can be held by, under the id its value is
    /// stored by: each furni by object id, each avatar by room index (players, pets and bots
    /// alike, as every user variable is keyed), and the single id 0 for the room and the
    /// context. The one enumeration; the holder list, the "with variable" selectors and the
    /// menu's liveness check all go through it, so none can key users another way.
    /// </summary>
    public IEnumerable<int> GetLiveTargetIds(WiredVariableTargetType targetType) =>
        targetType switch
        {
            WiredVariableTargetType.Furni => FurniModule.Items.Select(x => x.ObjectId.Value),
            WiredVariableTargetType.User => AvatarModule.Avatars.Select(x => x.ObjectId.Value),
            _ => [0],
        };

    private bool IsLiveTarget(WiredVariableBinding binding) =>
        binding.TargetType switch
        {
            WiredVariableTargetType.User => AvatarModule.TryGetAvatar(binding.TargetId, out _),
            WiredVariableTargetType.Furni => FurniModule.HasItem(binding.TargetId),
            WiredVariableTargetType.Global or WiredVariableTargetType.Context => true,
            _ => false,
        };

    private async Task ProcessInternalVariablesAsync(long now, CancellationToken ct)
    {
        var variables = _roomGrain
            ._wiredVariablesProvider.BuildVariablesForRoom(_roomGrain)
            .ToList();

        // The names some variables show beside their values are hotel texts: each family they
        // name is read, and only it, before any is described.
        foreach (
            var family in variables
                .OfType<WiredInternalVariable>()
                .Where(x => x.TextPrefix is not null)
                .GroupBy(x => x.TextPrefix!, System.StringComparer.Ordinal)
        )
        {
            var texts = await _roomGrain._hotelTextProvider.GetTextsByPrefixAsync(family.Key, ct);

            foreach (var variable in family)
                variable.UseTexts(texts);
        }

        foreach (var variable in variables)
            ProcessVariable(variable);
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

        if (!FurniModule.TryGetItem(boxId, out var item))
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
