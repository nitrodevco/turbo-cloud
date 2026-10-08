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

    public override int WiredCode => (int)WiredVariableBoxType.Reference;

    public override int GetMaxVariableIds() => 1;

    public override List<IWiredParamRule> GetIntParamRules() => [new WiredBoolParamRule(false)];

    private bool IsReadOnly => GetIntParamOrDefault(PARAM_READ_ONLY, false);

    protected override WiredVariableTargetType TargetType =>
        Reference?.TargetType ?? WiredVariableTargetType.Global;

    protected override WiredAvailabilityType AvailabilityType => WiredAvailabilityType.Reference;

    protected override WiredVariableFlags Flags
    {
        get
        {
            var flags = Reference?.Flags ?? WiredVariableFlags.None;

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
            _roomGrain._logger
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
    /// the room and the variable's kind are saved with the box.
    /// </summary>
    public override async Task<bool> ApplyWiredUpdateAsync(
        ActionContext ctx,
        UpdateWiredMessage update,
        CancellationToken ct
    )
    {
        if (ctx.Origin != ActionOrigin.System && ctx.PlayerId != _ctx.RoomObject.OwnerId)
            return false;

        ReferenceData? reference = null;

        if (
            update.VariableIds.Count > 0
            && WiredVariableId.TryParse(update.VariableIds[0], out var id)
        )
        {
            var shared = (
                await WiredSystem.GetOwnerSharedVariablesAsync(_ctx.RoomObject.OwnerId, ct)
            ).FirstOrDefault(x => x.Variable.VariableId == id);

            if (shared is not null)
                reference = new ReferenceData(
                    shared.RoomId,
                    id.ToString(),
                    shared.Variable.TargetType,
                    shared.Variable.Flags
                );
        }

        _reference = reference;

        if (reference is null)
            _ctx.RoomObject.ExtraData.DeleteSection(SECTION);
        else
            _ctx.RoomObject.ExtraData.UpdateSection(
                SECTION,
                JsonSerializer.SerializeToNode(reference)
            );

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

    /// <summary>Asks the room the variable lives in for its values, and to be told of its changes.</summary>
    protected override async Task FillInternalDataAsync(CancellationToken ct)
    {
        await base.FillInternalDataAsync(ct);

        _values = [];

        if (Reference is not { } reference || SourceVariableId is not { } id)
            return;

        var state = await _grainFactory
            .GetRoomGrain((RoomId)reference.RoomId)
            .SubscribeSharedWiredVariableAsync(id, _ctx.RoomId, ct);

        if (state is null)
            return;

        _values = new Dictionary<int, long>(state.Values);

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
            .LogAndForget(
                _roomGrain._logger,
                "change a shared variable of room {RoomId}",
                reference.RoomId
            );
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
