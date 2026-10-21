using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Logging;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.ExtraData;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Snapshots.Wired.Variables;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Wired.Rules;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Variables;

/// <summary>
/// "WIRED Variable: From Another Room" (Wired Faculty, variables-info #5): a variable of this
/// room that is a "Permanent, shared" user or global variable of another room of the box's owner.
/// The editor (<c>ReferenceVariable</c>) lists those rooms and their shared variables; the box
/// saves the picked variable's id, its own name and one int param, "read only", which keeps this
/// room from giving, taking or changing it. Only the box's owner may set it up.
///
/// The values live in the other room; this box holds a copy, filled when it loads and kept up to
/// date by every change there, and sends its own changes there. A change made there fires this
/// room's "variable changed" triggers as one from another room.
/// </summary>
[RoomObjectLogic("wf_var_reference")]
public class WiredVariableReference(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredVariableLogic(grainFactory, stuffDataFactory, ctx)
{
    private const string SECTION = "variable_reference";

    /// <summary>"Only the owner of this wired variable has the permission to make this change."</summary>
    public const string ERROR_OWNER_ONLY = "wiredfurni.error.variables.permissions.1";

    /// <summary>"Please select a valid reference variable."</summary>
    public const string ERROR_INVALID = "wiredfurni.error.variables.reference.valid";

    /// <summary>"Referencing to a variable in the same room is not allowed."</summary>
    public const string ERROR_SAME_ROOM = "wiredfurni.error.variables.reference.same_room";

    /// <summary>"A reference to this variable already exists in this room."</summary>
    public const string ERROR_ALREADY_EXISTS =
        "wiredfurni.error.variables.reference.already_exists";
    private const int PARAM_READ_ONLY = 0;

    /// <summary>The variable a box refers to, as saved with it.</summary>
    private sealed record ReferenceData(
        int RoomId,
        string VariableId,
        WiredVariableTargetType TargetType,
        WiredVariableFlags Flags
    );

    private ReferenceData? _reference;
    private Dictionary<int, long> _values = [];
    private ImmutableArray<WiredVariableSharedSnapshot> _sharedVariables = [];
    private SharedWiredVariableAddonsSnapshot? _sharedAddons;

    public override int WiredCode => (int)WiredVariableBoxType.Reference;

    public override int GetMaxVariableIds() => 1;

    // A fresh box is read only on the official server (defaults [1], 3/3 opens, e.g.
    // 2026-10-20T17-06-11-Open-wait; AS3 wired_setup/variables/ReferenceVariable.as:103).
    public override List<IWiredParamRule> GetIntParamRules() => [new WiredBoolParamRule(true)];

    private bool IsReadOnly => GetIntParamOrDefault(PARAM_READ_ONLY, true);

    protected override WiredVariableTargetType TargetType =>
        Reference?.TargetType ?? WiredVariableTargetType.Global;

    protected override WiredAvailabilityType AvailabilityType => WiredAvailabilityType.Reference;

    protected override WiredVariableFlags Flags
    {
        get
        {
            // Whether a text connector applies is this box's own (its stack, or what an import
            // add-on imports), not the source's.
            var flags =
                (Reference?.Flags ?? WiredVariableFlags.None)
                & ~WiredVariableFlags.HasTextConnector;

            return IsReadOnly
                ? flags
                    & ~(WiredVariableFlags.CanWriteValue | WiredVariableFlags.CanCreateAndDelete)
                : flags;
        }
    }

    private ReferenceData? Reference =>
        _reference ??= FurnitureExtraDataSections.Read<ReferenceData>(
            _ctx.RoomObject.ExtraData,
            SECTION,
            Logger
        );

    private WiredVariableId? SourceVariableId =>
        Reference is { } reference && WiredVariableId.TryParse(reference.VariableId, out var id)
            ? id
            : null;

    public override List<WiredVariableContextSnapshot> GetWiredContextSnapshots() =>
        [
            new WiredVariableSharedListSnapshot
            {
                ContextType = WiredContextType.SharedVariables,
                Variables = _sharedVariables,
            },
        ];

    public override async Task<bool> RefreshEditorContextAsync(CancellationToken ct)
    {
        _sharedVariables = await WiredSystem.GetOwnerSharedVariablesAsync(
            _ctx.RoomObject.OwnerId,
            ct
        );

        return true;
    }

    /// <summary>
    /// Only the box's owner sets it up, and only to a shared variable of one of their other rooms;
    /// the room and the variable's kind are saved with the box. A refusal names its hotel text
    /// instead of the generic one: someone else than the owner -> <c>wiredfurni.error.variables.permissions.1</c>;
    /// no variable picked, or one that is no shared variable of the owner's other rooms ->
    /// <c>reference.valid</c>; a variable of this room -> <c>reference.same_room</c>; a variable
    /// another reference box of this room already refers to -> <c>reference.already_exists</c>.
    /// Inference: which text Habbo sends for each case is read off the texts' wording; no official
    /// refusal of a reference box was recorded.
    /// </summary>
    public override async Task<bool> ApplyWiredUpdateAsync(
        ActionContext ctx,
        UpdateWiredMessage update,
        CancellationToken ct
    )
    {
        if (ctx.Origin != ActionOrigin.System && ctx.PlayerId != _ctx.RoomObject.OwnerId)
            throw new WiredSaveRefusedException(ERROR_OWNER_ONLY);

        // The editor sends "n" when no variable is picked (ReferenceVariable.readVariableIdsFromForm,
        // WiredVariable._-I2U, AS3 WIN63-202610131318).
        if (
            update.VariableIds.Count == 0
            || !WiredVariableId.TryParse(update.VariableIds[0], out var id)
        )
            throw new WiredSaveRefusedException(ERROR_INVALID);

        var shared = (
            await WiredSystem.GetOwnerSharedVariablesAsync(_ctx.RoomObject.OwnerId, ct)
        ).FirstOrDefault(x => x.Variable.VariableId == id);

        if (shared is null)
            throw new WiredSaveRefusedException(
                FurniModule.Items.Any(item =>
                    item.Logic is FurnitureWiredVariableLogic variable
                    && variable.GetVarSnapshot().VariableId == id
                )
                    ? ERROR_SAME_ROOM
                    : ERROR_INVALID
            );

        if (
            FurniModule.Items.Any(item =>
                item.Logic is WiredVariableReference other
                && !ReferenceEquals(other, this)
                && other.Reference is { } taken
                && taken.RoomId == shared.RoomId
                && taken.VariableId == id.ToString()
            )
        )
            throw new WiredSaveRefusedException(ERROR_ALREADY_EXISTS);

        var reference = new ReferenceData(
            shared.RoomId,
            id.ToString(),
            shared.Variable.TargetType,
            shared.Variable.Flags
        );

        _reference = reference;
        _ctx.RoomObject.ExtraData.UpdateSection(SECTION, JsonSerializer.SerializeToNode(reference));

        _values = [];

        return await base.ApplyWiredUpdateAsync(ctx, update, ct);
    }

    protected override bool GetValidVariableIds(
        List<string> proposed,
        out List<WiredVariableId> variableIds
    )
    {
        variableIds = SourceVariableId is { } id && proposed.Contains(id.ToString()) ? [id] : [];

        return true;
    }

    /// <summary>
    /// Asks the room the variable lives in for its values, and to be told of its changes, then
    /// takes over the add-ons it shares that this box imports.
    /// </summary>
    protected override async Task FillInternalDataAsync(CancellationToken ct)
    {
        await base.FillInternalDataAsync(ct);
        await SubscribeSourceAsync(ct);
        await ImportAddonsAsync(ct);
    }

    private async Task SubscribeSourceAsync(CancellationToken ct)
    {
        _values = [];
        _sharedAddons = null;
        _varSnapshot = null;

        if (Reference is not { } reference || SourceVariableId is not { } id)
            return;

        var state = await _grainFactory
            .GetRoomGrain((RoomId)reference.RoomId)
            .SubscribeSharedWiredVariableAsync(id, _ctx.RoomId, ct);

        if (state is null)
            return;

        _values = new Dictionary<int, long>(state.Values);
        _sharedAddons = state.Addons;
        _varSnapshot = null;

        // The variable there may have changed since this box was saved.
        if (
            state.Variable.Flags != reference.Flags
            || state.Variable.TargetType != reference.TargetType
        )
        {
            _reference = reference with
            {
                TargetType = state.Variable.TargetType,
                Flags = state.Variable.Flags,
            };
            _ctx.RoomObject.ExtraData.UpdateSection(
                SECTION,
                JsonSerializer.SerializeToNode(_reference)
            );
            _varSnapshot = null;
        }
    }

    /// <summary>Whether this box uses the shared variable <paramref name="variableId"/> of <paramref name="room"/>.</summary>
    public bool RefersTo(RoomId room, WiredVariableId variableId) =>
        Reference is { } reference
        && reference.RoomId == room.Value
        && SourceVariableId == variableId;

    /// <summary>
    /// What the variable's room shares of <paramref name="category"/> when an import add-on on this
    /// box's stack imports it ("place the Import Add-on on top of a WIRED Variable: From Another
    /// Room", Wired Faculty 19/10/2026), else null.
    /// </summary>
    private SharedWiredVariableAddonsSnapshot? Imported(WiredVariableAddonCategories category)
    {
        if (_sharedAddons is not { } shared || !shared.Categories.HasFlag(category))
            return null;

        return FurniModule
            .GetFloorItemsOnTile(_ctx.GetTileIdx())
            .Select(item => item.Logic)
            .OfType<Addons.WiredAddonVariableImport>()
            .Any(import => import.Categories.HasFlag(category))
            ? shared
            : null;
    }

    /// <summary>
    /// The subvariable creators and variable fx this box takes over from the variable's room: a
    /// copy of each shared one whose category the import add-on imports too, run on this box as
    /// if it stood on its stack. Their subvariables and fx are known by the shared boxes' ids,
    /// as there.
    /// </summary>
    public IReadOnlyList<Addons.FurnitureWiredAddonLogic> ImportedAddons => _importedAddons;

    private List<Addons.FurnitureWiredAddonLogic> _importedAddons = [];

    /// <summary>
    /// Builds <see cref="ImportedAddons"/> afresh from what the variable's room shares. An add-on
    /// of a kind this box's own stack has already is not taken over, as a text connector of its
    /// own goes before the imported one (inference: Habbo's choice between the two is not
    /// recorded). The fx system is told when fx come or go.
    /// </summary>
    private async Task ImportAddonsAsync(CancellationToken ct)
    {
        var hadFx = _importedAddons.Any(x => x is Addons.VariableFx.FurnitureWiredVariableFxLogic);

        _importedAddons = [];

        if (_sharedAddons is { Addons.Length: > 0 } shared)
        {
            var own = new HashSet<int>();

            foreach (var item in FurniModule.GetFloorItemsOnTile(_ctx.GetTileIdx()).ToList())
            {
                if (item.Logic is not Addons.FurnitureWiredAddonLogic addon)
                    continue;

                await addon.EnsureWiredLoadedAsync(ct);
                own.Add(addon.WiredCode);
            }

            foreach (var addon in shared.Addons)
            {
                if (
                    Imported(addon.Category) is null
                    || _roomGrain._logicProvider.CreateLogicInstance(addon.LogicType, _ctx)
                        is not Addons.FurnitureWiredAddonLogic copy
                    || own.Contains(copy.WiredCode)
                )
                    continue;

                await copy.ImportWiredAsync(addon, ct);
                _importedAddons.Add(copy);
            }
        }

        if (hadFx || _importedAddons.Any(x => x is Addons.VariableFx.FurnitureWiredVariableFxLogic))
            await _ctx.PublishRoomEventAsync(
                new WiredVariableFxBoxChangedEvent
                {
                    RoomId = _ctx.RoomId,
                    CausedBy = ActionContext.CreateForSystem(_ctx.RoomId),
                },
                ct
            );
    }

    /// <summary>A text connector on this box's own stack, else the imported ones.</summary>
    public override Dictionary<WiredVariableValue, string> GetTextConnectors()
    {
        var own = base.GetTextConnectors();

        return own.Count > 0
            ? own
            : Imported(WiredVariableAddonCategories.TextConnectors)?.TextConnectors ?? [];
    }

    public override bool HasTextConnector() =>
        base.HasTextConnector()
        || (Imported(WiredVariableAddonCategories.TextConnectors)?.HasTextConnector ?? false);

    public override string? GetTextConnectorFallback() =>
        base.GetTextConnectorFallback()
        ?? Imported(WiredVariableAddonCategories.TextConnectors)?.TextConnectorFallback;

    /// <summary>
    /// Who holds a value, as the room the variable lives in keeps it: a player by their id (they
    /// are users there too, under another room index), the room by 0. A pet or bot holds none.
    /// </summary>
    private bool TryGetHolder(in WiredVariableKey key, out int holderId)
    {
        holderId = 0;

        if (!CanBind(key) || Reference is null)
            return false;

        if (key.TargetType == WiredVariableTargetType.Global)
            return true;

        if (
            !AvatarModule.TryGetAvatar(RoomObjectId.Parse(key.TargetId), out var avatar)
            || avatar is not IRoomPlayer player
        )
            return false;

        holderId = player.PlayerId.Value;

        return true;
    }

    public override bool TryGetValue(in WiredVariableKey key, out WiredVariableValue value)
    {
        value = WiredVariableValue.Default;

        if (!TryGetHolder(key, out var holderId))
            return false;

        if (_values.TryGetValue(holderId, out var stored))
        {
            value = new WiredVariableValue(stored);

            return true;
        }

        // A global holds its 0 before anything is stored for it, there as here.
        if (key.TargetType == WiredVariableTargetType.Global)
        {
            value = new WiredVariableValue(0);

            return true;
        }

        return false;
    }

    public override Task<bool> GiveValueAsync(
        WiredVariableKey key,
        WiredVariableValue value,
        bool replace = false
    )
    {
        if (
            !Flags.Has(WiredVariableFlags.CanCreateAndDelete)
            || !TryGetHolder(key, out var holderId)
        )
            return Task.FromResult(false);

        var existed = _values.TryGetValue(holderId, out var previous);

        if (existed && !replace)
            return Task.FromResult(false);

        _values[holderId] = value.Value;

        PublishChange(
            key,
            existed ? WiredVariableChangeType.Updated : WiredVariableChangeType.Created,
            value,
            existed ? new WiredVariableValue(previous) : value
        );
        SendToSource(WiredVariableChangeType.Created, holderId, value.Value, replace);

        return Task.FromResult(true);
    }

    public override Task<bool> SetValueAsync(
        IWiredExecutionContext ctx,
        WiredVariableKey key,
        WiredVariableValue value
    )
    {
        if (!Flags.Has(WiredVariableFlags.CanWriteValue) || !TryGetHolder(key, out var holderId))
            return Task.FromResult(false);

        if (
            !_values.TryGetValue(holderId, out var previous)
            && key.TargetType != WiredVariableTargetType.Global
        )
            return Task.FromResult(false);

        _values[holderId] = value.Value;

        PublishChange(
            key,
            WiredVariableChangeType.Updated,
            value,
            new WiredVariableValue(previous)
        );
        SendToSource(WiredVariableChangeType.Updated, holderId, value.Value);

        return Task.FromResult(true);
    }

    public override bool RemoveValue(WiredVariableKey key)
    {
        if (
            !Flags.Has(WiredVariableFlags.CanCreateAndDelete)
            || !TryGetHolder(key, out var holderId)
            || !_values.Remove(holderId, out var previous)
        )
            return false;

        var value = new WiredVariableValue(previous);

        PublishChange(key, WiredVariableChangeType.Removed, value, value);
        SendToSource(WiredVariableChangeType.Removed, holderId, previous);

        return true;
    }

    /// <summary>Applies a change this room made where the variable lives. Fire-and-forget.</summary>
    private void SendToSource(
        WiredVariableChangeType changeType,
        int holderId,
        long value,
        bool replace = false
    )
    {
        if (Reference is not { } reference || SourceVariableId is not { } id)
            return;

        _grainFactory
            .GetRoomGrain((RoomId)reference.RoomId)
            .ChangeSharedWiredVariableAsync(
                id,
                new SharedWiredVariableChange
                {
                    ChangeType = changeType,
                    HolderId = holderId,
                    Value = value,
                    Replace = replace,
                },
                _ctx.RoomId,
                CancellationToken.None
            )
            .LogAndForget(Logger, "change a shared variable of room {RoomId}", reference.RoomId);
    }

    /// <summary>
    /// The variable changed in the room it lives in (or through another room using it): the copy
    /// follows, and this room's triggers hear of it as a change from another room.
    /// </summary>
    public void OnSourceChanged(
        RoomId sourceRoom,
        WiredVariableId variableId,
        SharedWiredVariableChange change
    )
    {
        if (
            Reference is not { } reference
            || reference.RoomId != sourceRoom.Value
            || SourceVariableId != variableId
        )
            return;

        var existed = _values.TryGetValue(change.HolderId, out var previous);

        if (change.ChangeType == WiredVariableChangeType.Removed)
            _values.Remove(change.HolderId);
        else
            _values[change.HolderId] = change.Value;

        var storedKey = new WiredVariableKey(_variableId, reference.TargetType, change.HolderId);

        if (!TryGetLiveKey(storedKey, out var key))
            return;

        PublishChange(
            key,
            change.ChangeType == WiredVariableChangeType.Created && existed
                ? WiredVariableChangeType.Updated
                : change.ChangeType,
            new WiredVariableValue(change.Value),
            new WiredVariableValue(existed ? previous : change.Previous),
            WiredVariableChangeOriginType.AnotherRoom
        );
    }
}
