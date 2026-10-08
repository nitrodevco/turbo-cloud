using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Outgoing.Notifications;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Snapshots.Mapping;

namespace Turbo.Rooms.Grains;

public sealed partial class RoomGrain
{
    /// <summary>The notification a refused floor plan answers with.</summary>
    internal const string FLOOR_PLAN_ERROR_NOTIFICATION = "floorplan_editor.error";

    public async Task<ImmutableArray<PlayerId>?> SaveFloorPlanAsync(
        ActionContext ctx,
        string modelData,
        FloorPlanPropertiesSnapshot? properties,
        CancellationToken ct
    )
    {
        try
        {
            // Redrawing the room is the owner's alone: it moves everybody in it and can send
            // other people's furni home.
            if (await SecurityModule.GetControllerLevelAsync(ctx) < RoomControllerType.Owner)
            {
                _logger.LogWarning(
                    "Player {PlayerId} may not redraw room {RoomId}: they do not own it",
                    ctx.PlayerId,
                    _state.RoomId
                );

                return null;
            }

            // The editor greys out its own save button without a membership
            // (BCFloorPlanEditor reads the Builders Club countdown for exactly this), so a save
            // arriving without one did not come from the editor.
            if (
                !await _grainFactory
                    .GetPlayerSubscriptionGrain(ctx.PlayerId)
                    .HasActiveAsync(SubscriptionType.BuildersClub, ct)
                && !await SecurityModule.HasPermissionAsync(
                    ctx.PlayerId,
                    PermissionNodes.Room.FLOORPLAN_SAVE_WITHOUT_CLUB
                )
            )
            {
                _logger.LogWarning(
                    "Player {PlayerId} may not redraw room {RoomId}: they hold no Builders Club membership",
                    ctx.PlayerId,
                    _state.RoomId
                );

                return null;
            }

            // Past the area the editor draws, only with the perk it draws it for.
            var allowLarge = await SecurityModule.HasPermissionAsync(
                ctx.PlayerId,
                PermissionNodes.Room.FLOORPLAN_LARGE
            );

            var error = await MapModule.SaveFloorPlanAsync(modelData, properties, allowLarge, ct);

            if (error is not null)
            {
                // `floorplan_editor.error`: the hotel's "Floor plan validation failed" pop-up,
                // its `%ERRORS%` the texts the plan broke.
                await _grainFactory.SendComposerToPlayerAsync(
                    ctx.PlayerId,
                    new NotificationDialogMessageComposer
                    {
                        NotificationType = FLOOR_PLAN_ERROR_NOTIFICATION,
                        Parameters = ImmutableDictionary<string, string>.Empty.Add("errors", error),
                    },
                    ct
                );

                return null;
            }

            return
            [
                .. _state.AvatarsByObjectId.Values.OfType<IRoomPlayer>().Select(x => x.PlayerId),
            ];
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to save the floor plan of room {RoomId} for player {PlayerId}",
                _state.RoomId,
                ctx.PlayerId
            );

            return null;
        }
    }

    /// <summary>
    /// Keeps the wall and floor settings a floor plan was saved with. They live on the room
    /// rather than on the model, because two rooms on the same plan may be drawn differently,
    /// and they go through the settings path so the listing and everyone in the room hear of it.
    /// </summary>
    internal async Task ApplyFloorPlanSettingsAsync(
        FloorPlanPropertiesSnapshot properties,
        CancellationToken ct
    )
    {
        var current = _state.RoomSnapshot;

        // The editor sends one of four thickness settings and a slider value it cannot push past
        // its own ceiling; anything else did not come from the editor, so the room keeps what it
        // had rather than taking it.
        var next = current with
        {
            WallThickness = Enum.IsDefined((RoomThicknessType)properties.WallThickness)
                ? (RoomThicknessType)properties.WallThickness
                : current.WallThickness,
            FloorThickness = Enum.IsDefined((RoomThicknessType)properties.FloorThickness)
                ? (RoomThicknessType)properties.FloorThickness
                : current.FloorThickness,
            WallHeight =
                properties.FixedWallsHeight < 0
                    ? -1
                    : Math.Min(properties.FixedWallsHeight, _roomConfig.FloorPlanMaxWallHeight),
        };

        if (
            next.WallThickness == current.WallThickness
            && next.FloorThickness == current.FloorThickness
            && next.WallHeight == current.WallHeight
        )
            return;

        try
        {
            await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

            var entity = await dbCtx.Rooms.FirstOrDefaultAsync(
                x => x.Id == _state.RoomId.Value,
                ct
            );

            if (entity is null)
                return;

            entity.ThicknessWall = next.WallThickness;
            entity.ThicknessFloor = next.FloorThickness;
            entity.WallHeight = next.WallHeight;

            await dbCtx.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            // The plan itself is already saved; losing the trimmings is not worth undoing it.
            _logger.LogError(
                ex,
                "Failed to save the wall and floor settings of room {RoomId}",
                _state.RoomId
            );

            return;
        }

        await ApplySettingsAsync(current, next, ct);
    }
}
