using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Turbo.Logging;
using Turbo.Primitives;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture;
using Turbo.Primitives.Furniture.Snapshots.StuffData;
using Turbo.Primitives.Inventory.Grains;
using Turbo.Primitives.Inventory.Snapshots;
using Turbo.Primitives.Messages.Outgoing.Room.Furniture;
using Turbo.Primitives.Moderation;
using Turbo.Primitives.Movements;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Object;
using Turbo.Rooms.Object.Logic.Furniture.Wall;

namespace Turbo.Rooms.Grains.Modules;

/// <summary>
/// Notes from post-it pads. A pad is a stack of sheets (<see cref="PostItPads"/>), and each sheet
/// stuck to a wall is a new note of the pad's kind belonging to the room's owner: Flash offers a
/// note's edit and delete to the room's owner and its controllers, never to whoever wrote it
/// (<c>FurnitureStickieWidgetHandler</c>). A builder sticks one straight up (<c>PlacePostIt</c>);
/// anyone else only in a room with a post-it wall, through the editor its request opens
/// (<c>PlacePostIt</c> -> <c>RequestSpamWallPostIt</c> -> <c>AddSpamWallPostIt</c>). The room
/// checks what it can before a sheet is taken, and gives the sheet back when no note came of
/// it, so the pad and the room never disagree.
/// </summary>
public sealed partial class RoomActionModule
{
    public async Task<bool> PlacePostItAsync(
        ActionContext ctx,
        RoomObjectId padId,
        string location,
        CancellationToken ct
    )
    {
        if (!WallPosition.TryParse(location, out var position))
            return RejectPostIt(ctx, padId, "the location is no wall position");

        var inventory = GrainFactory.GetInventoryGrain(ctx.PlayerId);

        if (!await SecurityModule.CanPlaceFurniAsync(ctx))
        {
            if (!HasSpamWall())
                throw new TurboException(TurboErrorCodeEnum.NoPermissionToPlaceFurni);

            // A post-it wall lets anyone leave a note: their client opens the editor on their
            // pad, at the spot they chose, and the note comes back as AddSpamWallPostIt.
            var pad = await inventory.GetItemSnapshotAsync(padId, ct);

            if (pad is null || !PostItPads.IsPad(pad.Definition))
                return RejectPostIt(ctx, padId, "no post-it pad of theirs");

            await GrainFactory.SendComposerToPlayerAsync(
                ctx.PlayerId,
                new RequestSpamWallPostItMessageComposer { ItemId = padId, Location = location },
                ct
            );

            return true;
        }

        return await StickPostItSheetAsync(
            ctx,
            inventory,
            padId,
            position,
            StickieColors.DEFAULT,
            ct
        );
    }

    public async Task<bool> AddSpamWallPostItAsync(
        ActionContext ctx,
        RoomObjectId padId,
        string location,
        string color,
        string text,
        CancellationToken ct
    )
    {
        if (!HasSpamWall())
            return RejectPostIt(ctx, padId, "the room has no post-it wall");

        var config = RoomConfig;

        if (
            !StickieColors.IsValid(color)
            || text.Length == 0
            || text.Length > config.StickieTextMaxLength
            || !WallPosition.TryParse(location, out var position)
        )
            return RejectPostIt(ctx, padId, "colour, text or location invalid");

        text = _roomGrain._wordFilter.FilterAndTruncate(text, config.StickieTextMaxLength);

        return await StickPostItSheetAsync(
            ctx,
            GrainFactory.GetInventoryGrain(ctx.PlayerId),
            padId,
            position,
            StickieColors.Compose(color.ToUpperInvariant(), text),
            ct
        );
    }

    private bool HasSpamWall() => FurniModule.Items.Any(x => x.Logic is FurnitureSpamWallLogic);

    /// <summary>
    /// One sheet off the pad, then the note made of it. The pad's last sheet uses the pad up
    /// first (seq 0), so the note follows it; a note the room refuses is destroyed after it, and
    /// the sheet is given back under the operation's refund.
    /// </summary>
    private async Task<bool> StickPostItSheetAsync(
        ActionContext ctx,
        IInventoryGrain inventory,
        RoomObjectId padId,
        WallPosition position,
        string noteData,
        CancellationToken ct
    )
    {
        var cause = MovementCause.New(MovementCauses.ROOM_POST_IT, ctx.PlayerId.Value, padId.Value);
        var taken = await inventory.TakePostItSheetAsync(padId, cause, ct);

        if (taken is null)
            return RejectPostIt(ctx, padId, "no post-it pad of theirs");

        bool stuck;

        try
        {
            stuck = await FurniModule.CreateWallItemAsync(
                ctx,
                taken.Pad.Definition,
                State.RoomSnapshot.OwnerId,
                position,
                PostItPads.NoteExtraData(noteData),
                new LegacyStuffSnapshot { StuffBitmask = 0, Data = noteData },
                cause.StartingAt(cause.FirstSeq + (taken.UsedUpPad ? 1 : 0)),
                ct
            );
        }
        catch (Exception)
        {
            await GivePostItSheetBackAsync(ctx, inventory, taken, cause);

            throw;
        }

        if (!stuck)
            await GivePostItSheetBackAsync(ctx, inventory, taken, cause);

        return stuck;
    }

    /// <summary>
    /// No note came of a sheet: the pad gets it back. Not cancelled with the request that took
    /// it, and a give-back that fails is logged with what was lost: there is nowhere else to put it.
    /// </summary>
    private async Task GivePostItSheetBackAsync(
        ActionContext ctx,
        IInventoryGrain inventory,
        PostItSheetTakenSnapshot taken,
        MovementCause cause
    )
    {
        try
        {
            await inventory.ReturnPostItSheetAsync(
                taken,
                cause.ForRefund(),
                CancellationToken.None
            );
        }
        catch (Exception ex)
        {
            Logger.LogError(
                ex,
                "Failed to give player {PlayerId} back a sheet of post-it pad {ItemId} (definition {DefinitionId}) in room {RoomId}",
                ctx.PlayerId,
                taken.Pad.ItemId,
                taken.Pad.Definition.Id,
                _roomGrain.RoomId
            );
        }
    }

    /// <summary>Logs a refused note with the ids that identify it, and yields false.</summary>
    private bool RejectPostIt(ActionContext ctx, RoomObjectId padId, string reason)
    {
        Logger.LogWarning(
            "Rejected a note from post-it pad {ItemId} in room {RoomId} by player {PlayerId}: {Reason}",
            padId,
            _roomGrain.RoomId,
            ctx.PlayerId,
            reason
        );

        return false;
    }
}
