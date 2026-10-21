using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Turbo.Logging;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Primitives.Rooms.Snapshots.Wired.Variables;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Variables;

namespace Turbo.Rooms.Grains.Systems;

/// <summary>
/// Shared variables ("Permanent, shared") and "WIRED Variable: From Another Room" (Wired Faculty,
/// variables-info #5): a room keeps the values of its shared variables; another room of the same
/// owner may use one through a reference box, which keeps a copy that every change is sent to.
/// A change made through a reference is applied where the variable lives and passed on to the
/// other rooms using it. Only rooms loaded at the time hear of a change; a room that loads later
/// reads the values afresh.
/// </summary>
public sealed partial class RoomWiredSystem
{
    /// <summary>The classnames of the boxes that can make a shared variable.</summary>
    private static readonly string[] SHARED_VARIABLE_BOXES = ["wf_var_user", "wf_var_room"];

    /// <summary>The shared variable boxes of the room, each with its wired data loaded.</summary>
    private async Task<List<FurnitureWiredVariableLogic>> SharedVariableBoxesAsync(
        CancellationToken ct
    )
    {
        var boxes = new List<FurnitureWiredVariableLogic>();

        foreach (
            var box in FurniModule.Items.Select(x => x.Logic).OfType<FurnitureWiredVariableLogic>()
        )
        {
            await box.EnsureWiredLoadedAsync(ct);

            if (box.IsShared)
                boxes.Add(box);
        }

        return boxes;
    }

    private async Task<FurnitureWiredVariableLogic?> GetSharedVariableBoxAsync(
        WiredVariableId variableId,
        CancellationToken ct
    ) =>
        (await SharedVariableBoxesAsync(ct)).FirstOrDefault(x =>
            x.GetVarSnapshot().VariableId == variableId
        );

    public async Task<ImmutableArray<WiredVariableSnapshot>> GetSharedVariablesAsync(
        CancellationToken ct
    ) => [.. (await SharedVariableBoxesAsync(ct)).Select(x => x.GetVarSnapshot())];

    public async Task<SharedWiredVariableStateSnapshot?> SubscribeSharedVariableAsync(
        WiredVariableId variableId,
        RoomId referrer,
        CancellationToken ct
    )
    {
        if (await GetSharedVariableBoxAsync(variableId, ct) is not { } box)
            return null;

        // The share add-on's last Synchronize is kept with it; it must be loaded to read it.
        if (box.GetShareAddon() is { } share)
            await share.EnsureWiredLoadedAsync(ct);

        return box.SubscribeShared(referrer);
    }

    public async Task<bool> ApplySharedVariableChangeAsync(
        WiredVariableId variableId,
        SharedWiredVariableChange change,
        RoomId origin,
        CancellationToken ct
    ) =>
        await GetSharedVariableBoxAsync(variableId, ct) is { } box
        && await box.ApplySharedChangeAsync(change, origin);

    /// <summary>
    /// Sends a change of one of this room's shared variables to the rooms using it that are loaded
    /// now, except the one it came from (which made it already). Fire-and-forget: a room that does
    /// not answer misses the change and reads the value afresh when it next loads.
    /// </summary>
    public void ShareVariableChange(
        WiredVariableId variableId,
        SharedWiredVariableChange change,
        IReadOnlyCollection<int> referrers,
        RoomId? except
    ) =>
        ShareVariableChangeAsync(variableId, change, referrers, except)
            .LogAndForget(Logger, "share a variable change of room {RoomId}", _roomGrain.RoomId);

    private async Task ShareVariableChangeAsync(
        WiredVariableId variableId,
        SharedWiredVariableChange change,
        IReadOnlyCollection<int> referrers,
        RoomId? except
    )
    {
        var active = (
            await GrainFactory.GetRoomDirectoryGrain().GetActiveRoomIdsAsync(default)
        ).ToHashSet();

        foreach (var referrer in referrers)
        {
            var roomId = (RoomId)referrer;

            if (roomId == except || roomId == _roomGrain.RoomId || !active.Contains(roomId))
                continue;

            _roomGrain
                ._grainFactory.GetRoomGrain(roomId)
                .OnSharedWiredVariableChangedAsync(_roomGrain.RoomId, variableId, change, default)
                .LogAndForget(Logger, "send a shared variable change to room {RoomId}", roomId);
        }
    }

    /// <summary>
    /// "Synchronize" on a share add-on: it takes what it shares off the stack as it stands now
    /// (the add-ons of the categories it ticks now) and keeps that until the next Synchronize, and
    /// the loaded rooms using the shared variable are told to read it again. A room that loads
    /// later reads it when it subscribes. What the button sends is AS3
    /// <c>VariableAddonShareAddon.synchronize</c> (WIN63-202610201032); that the importing room
    /// shows only what the last one took, edits and Share ticks since notwithstanding, is the
    /// official client capture lab61 (c) (text connectors; the other two categories are taken
    /// the same way by inference).
    /// </summary>
    public async Task<bool> SynchronizeSharedAddonsAsync(int boxId, CancellationToken ct)
    {
        if (
            !FurniModule.TryGetItem(boxId, out var item)
            || item.Logic is not WiredAddonVariableShare share
        )
            return false;

        await share.EnsureWiredLoadedAsync(ct);

        if (share.GetSharedVariable() is not { } variable)
            return false;

        await variable.LoadTileAddonsAsync(ct);
        share.Synchronize(variable.BuildSharedAddons(share));

        await ShareAddonsChangedAsync(
            variable.GetVarSnapshot().VariableId,
            variable.SharedReferrers
        );

        return true;
    }

    private async Task ShareAddonsChangedAsync(
        WiredVariableId variableId,
        IReadOnlyCollection<int> referrers
    )
    {
        var active = (
            await GrainFactory.GetRoomDirectoryGrain().GetActiveRoomIdsAsync(default)
        ).ToHashSet();

        foreach (var referrer in referrers)
        {
            var roomId = (RoomId)referrer;

            if (roomId == _roomGrain.RoomId || !active.Contains(roomId))
                continue;

            _roomGrain
                ._grainFactory.GetRoomGrain(roomId)
                .OnSharedWiredVariableAddonsChangedAsync(_roomGrain.RoomId, variableId, default)
                .LogAndForget(Logger, "send shared variable add-ons to room {RoomId}", roomId);
        }
    }

    /// <summary>
    /// The add-ons a shared variable of another room shares were synchronized: the reference boxes
    /// using it are loaded again on the next tick, which reads them afresh.
    /// </summary>
    public Task OnSharedVariableAddonsChangedAsync(
        RoomId sourceRoom,
        WiredVariableId variableId,
        CancellationToken ct
    )
    {
        List<int> boxIds =
        [
            .. FurniModule
                .Items.Where(x =>
                    x.Logic is WiredVariableReference reference
                    && reference.RefersTo(sourceRoom, variableId)
                )
                .Select(x => x.ObjectId.Value),
        ];

        if (boxIds.Count == 0)
            return Task.CompletedTask;

        return _roomGrain.PublishRoomEventAsync(
            new WiredVariableBoxChangedEvent
            {
                RoomId = _roomGrain.RoomId,
                CausedBy = ActionContext.CreateForSystem(_roomGrain.RoomId),
                BoxIds = boxIds,
            },
            ct
        );
    }

    /// <summary>A shared variable of another room changed: the reference boxes using it follow.</summary>
    public void OnSharedVariableChanged(
        RoomId sourceRoom,
        WiredVariableId variableId,
        SharedWiredVariableChange change
    )
    {
        foreach (
            var reference in FurniModule.Items.Select(x => x.Logic).OfType<WiredVariableReference>()
        )
            reference.OnSourceChanged(sourceRoom, variableId, change);
    }

    /// <summary>
    /// The shared variables a reference box of <paramref name="ownerId"/> may use: those of the
    /// owner's other rooms. A room is asked only when it may have one: it is loaded (its boxes may
    /// not be saved yet), or a saved variable box in it says "Permanent, shared".
    /// </summary>
    public async Task<ImmutableArray<WiredVariableSharedSnapshot>> GetOwnerSharedVariablesAsync(
        PlayerId ownerId,
        CancellationToken ct
    )
    {
        Dictionary<int, string> rooms;
        HashSet<int> candidates;

        await using (var db = await _roomGrain._dbCtxFactory.CreateDbContextAsync(ct))
        {
            rooms = await db
                .Rooms.Where(x =>
                    x.PlayerEntityId == ownerId.Value && x.Id != _roomGrain.RoomId.Value
                )
                .ToDictionaryAsync(x => x.Id, x => x.Name, ct);

            var roomIds = rooms.Keys.ToList();
            var boxes = await db
                .Furnitures.Where(x =>
                    x.RoomEntityId != null
                    && roomIds.Contains(x.RoomEntityId.Value)
                    && SHARED_VARIABLE_BOXES.Contains(x.FurnitureDefinitionEntity!.Name)
                )
                .Select(x => new { RoomId = x.RoomEntityId!.Value, x.ExtraData })
                .ToListAsync(ct);

            candidates = [.. boxes.Where(x => SavedAsShared(x.ExtraData)).Select(x => x.RoomId)];
        }

        var active = await _roomGrain
            ._grainFactory.GetRoomDirectoryGrain()
            .GetActiveRoomIdsAsync(ct);

        candidates.UnionWith(active.Select(x => x.Value).Where(rooms.ContainsKey));

        var result = ImmutableArray.CreateBuilder<WiredVariableSharedSnapshot>();

        foreach (var roomId in candidates.OrderBy(x => x))
        {
            var variables = await _roomGrain
                ._grainFactory.GetRoomGrain((RoomId)roomId)
                .GetSharedWiredVariablesAsync(ct);

            foreach (var variable in variables)
                result.Add(
                    new WiredVariableSharedSnapshot
                    {
                        ContextType = WiredContextType.SharedVariables,
                        RoomId = roomId,
                        RoomName = rooms[roomId],
                        Variable = variable,
                    }
                );
        }

        return result.ToImmutable();
    }

    /// <summary>Whether a variable box's saved extra data has "Permanent, shared" as its availability.</summary>
    private static bool SavedAsShared(string? extraData)
    {
        if (string.IsNullOrEmpty(extraData))
            return false;

        try
        {
            using var json = JsonDocument.Parse(extraData);

            return json.RootElement.TryGetProperty(ExtraDataSectionType.WIRED, out var wired)
                && wired.TryGetProperty("IntParams", out var intParams)
                && intParams.ValueKind == JsonValueKind.Array
                && intParams.GetArrayLength() > 0
                && intParams[0].TryGetInt32(out var availability)
                && availability == (int)WiredAvailabilityType.Shared;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
