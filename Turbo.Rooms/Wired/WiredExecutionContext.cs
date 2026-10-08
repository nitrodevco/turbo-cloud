using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.Snapshots.StuffData;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Object.Furniture;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Furniture.Wall;
using Turbo.Primitives.Rooms.Snapshots.Wired;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired;

public sealed class WiredExecutionContext(RoomGrain roomGrain)
    : WiredContext(roomGrain),
        IWiredExecutionContext
{
    public DateTimeOffset RoomLocalTime => WiredSystem.GetRoomLocalTime();

    /// <summary>Where "Change Variable Value" holds its change back; null when it changes at once.</summary>
    internal WiredVariableChangeBatch? VariableChanges { get; init; }

    public List<WiredUserMovementSnapshot> UserMoves { get; } = [];
    public List<WiredFloorItemMovementSnapshot> FloorItemMoves { get; } = [];
    public List<WiredWallItemMovementSnapshot> WallItemMoves { get; } = [];
    public List<WiredUserDirectionSnapshot> UserDirections { get; } = [];
    public List<(RoomObjectId, StuffDataSnapshot)> FloorItemStateUpdates { get; } = [];
    public List<(RoomObjectId, string)> WallItemStateUpdates { get; } = [];

    public async Task ProcessItemStateUpdateAsync(IRoomItem item, int state)
    {
        if (item is null)
            return;

        try
        {
            await item.Logic.SetStateAsync(state, false);

            switch (item)
            {
                case IRoomFloorItem:
                    FloorItemStateUpdates.Add((item.ObjectId, item.Logic.StuffData.GetSnapshot()));
                    break;
                case IRoomWallItem:
                    WallItemStateUpdates.Add((item.ObjectId, item.Logic.GetLegacyString()));
                    break;
            }
        }
        catch (Exception ex)
        {
            _roomGrain._logger.LogWarning(
                ex,
                "Wired could not set state {State} on item {ItemId} in room {RoomId}",
                state,
                item.ObjectId,
                _roomGrain.RoomId
            );
        }
    }

    public async Task<bool> TryMoveFloorItemAsync(
        IRoomFloorItem floorItem,
        int x,
        int y,
        Altitude? z = null,
        Rotation? rotation = null
    ) =>
        FurniModule.CanPlaceFloorItem(floorItem, x, y, rotation ?? floorItem.Rotation)
        && await ProcessFloorItemMovementAsync(floorItem, MapModule.ToIdx(x, y), z, rotation);

    public async Task<bool> ProcessFloorItemMovementAsync(
        IRoomFloorItem floorItem,
        int tileIdx,
        Altitude? z,
        Rotation? rot
    )
    {
        if (floorItem is null)
            return false;

        try
        {
            var (sourceX, sourceY, sourceZ) = (floorItem.X, floorItem.Y, floorItem.Z);
            var sourceIdx = MapModule.ToIdx(sourceX, sourceY);
            var carried = CollectCarriedAvatars(floorItem);

            if (Policy.MovePhysics.HasFlag(WiredMovePhysicsFlags.KeepAltitude))
                z ??= floorItem.Z;

            // A projectile addon speaks only for the furni it picked as projectiles.
            var projectile =
                Policy.Projectile is { } settings
                && settings.ProjectileIds.Contains(floorItem.ObjectId)
                    ? settings
                    : null;
            var (flightToX, flightToY) = MapModule.GetTileXY(tileIdx);
            var (flightX, flightY) = (flightToX - sourceX, flightToY - sourceY);

            if (
                projectile?.RotateBy is { } system
                && WiredDirections.Resolve(system, flightX, flightY) is { } facing
            )
                rot = facing.Rotate(projectile.RotationOffset);

            // Through the furni module, so the item's logic hears of the move as it would from a
            // player; only the announcing stays here, batched into the action's one packet.
            if (
                !await FurniModule.MoveFloorItemAsync(
                    AsActionContext(),
                    floorItem,
                    tileIdx,
                    z,
                    rot,
                    announce: false,
                    CancellationToken
                )
            )
                return false;

            if (projectile is not null)
                WiredSystem.BeginProjectileFlight(
                    floorItem.ObjectId,
                    sourceX,
                    sourceY,
                    sourceZ.ToInt(),
                    floorItem.X,
                    floorItem.Y,
                    floorItem.Z.ToInt(),
                    GetAnimationTime()
                );

            FloorItemMoves.Add(
                new()
                {
                    ObjectId = floorItem.ObjectId,
                    SourceX = sourceX,
                    SourceY = sourceY,
                    SourceZ = sourceZ,
                    TargetX = floorItem.X,
                    TargetY = floorItem.Y,
                    TargetZ = floorItem.Z,
                    Rotation = floorItem.Rotation,
                    AnimationTime = GetAnimationTime(),
                    // The movement curve addon outranks the projectile's own trajectory.
                    CurveStrength = Policy.JumpStrength ?? projectile?.CurveStrength,
                    OvershootDistance = projectile?.Distance switch
                    {
                        WiredProjectileDistanceType.Overshoot => projectile.DistanceTiles,
                        // The tiles still missing from the distance it always flies.
                        WiredProjectileDistanceType.Fixed => projectile.DistanceTiles
                            - Math.Max(Math.Abs(flightX), Math.Abs(flightY)),
                        _ => null,
                    },
                }
            );

            if (carried.Count > 0)
            {
                var dx = floorItem.X - sourceX;
                var dy = floorItem.Y - sourceY;

                foreach (var avatar in carried)
                {
                    var targetX = avatar.X + dx;
                    var targetY = avatar.Y + dy;

                    if (!MapModule.InBounds(targetX, targetY))
                        continue;

                    await ProcessUserMovementAsync(
                        avatar,
                        MapModule.ToIdx(targetX, targetY),
                        SlideAvatarMoveType.Slide
                    );
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            _roomGrain._logger.LogWarning(
                ex,
                "Wired could not move item {ItemId} to tile {TileIdx} in room {RoomId}",
                floorItem.ObjectId,
                tileIdx,
                _roomGrain.RoomId
            );

            return false;
        }
    }

    public async Task ProcessWallItemMovementAsync(
        IRoomWallItem wallItem,
        int x,
        int y,
        Altitude z,
        Rotation rot,
        int wallOffset
    )
    {
        if (wallItem is null)
            return;

        try
        {
            var (sourceX, sourceY, sourceZ, sourceOffset) = (
                wallItem.X,
                wallItem.Y,
                wallItem.Z,
                wallItem.WallOffset
            );

            if (
                await FurniModule.MoveWallItemAsync(
                    AsActionContext(),
                    wallItem,
                    x,
                    y,
                    z,
                    wallOffset,
                    rot,
                    announce: false,
                    CancellationToken
                )
            )
            {
                WallItemMoves.Add(
                    new()
                    {
                        ObjectId = wallItem.ObjectId,
                        IsDirectionRight = wallItem.Rotation != Rotation.South,
                        SourceX = sourceX,
                        SourceY = sourceY,
                        SourceOffsetX = sourceOffset,
                        SourceOffsetY = (int)sourceZ,
                        TargetX = wallItem.X,
                        TargetY = wallItem.Y,
                        TargetOffsetX = wallItem.WallOffset,
                        TargetOffsetY = (int)wallItem.Z,
                        AnimationTime = GetAnimationTime(),
                    }
                );
            }
        }
        catch (Exception ex)
        {
            _roomGrain._logger.LogWarning(
                ex,
                "Wired could not move wall item {ItemId} in room {RoomId}",
                wallItem.ObjectId,
                _roomGrain.RoomId
            );
        }
    }

    public Task<bool> ProcessUserMovementAsync(
        IRoomAvatar avatar,
        int tileIdx,
        SlideAvatarMoveType moveType
    ) => ProcessUserMovementAsync(avatar, tileIdx, moveType, instant: false);

    public async Task<bool> ProcessUserMovementAsync(
        IRoomAvatar avatar,
        int tileIdx,
        SlideAvatarMoveType moveType,
        bool instant
    )
    {
        if (avatar is null)
            return false;

        try
        {
            var map = MapModule;

            if (!map.InBounds(tileIdx))
                return false;

            var sourceIdx = map.ToIdx(avatar.X, avatar.Y);

            if (sourceIdx == tileIdx)
                return true;

            // A teleport puts the avatar down at once instead of walking it there, so users already
            // on the tile are no obstacle: every user a selection picked can be teleported onto
            // one furni. A slide or a move still stops at a user unless the movement physics let
            // it through, and a closed tile or a furni that cannot be stood on refuses either.
            if (
                !Policy.MovePhysics.HasFlag(WiredMovePhysicsFlags.MoveThroughUsers)
                && !map.CanAvatarWalk(
                    avatar,
                    tileIdx,
                    true,
                    ignoreAvatars: moveType == SlideAvatarMoveType.None
                )
            )
                return false;

            if (
                Policy.MovePhysics.HasFlag(WiredMovePhysicsFlags.MoveThroughUsers)
                && map.IsTileDisabled(tileIdx)
            )
                return false;

            var (sourceX, sourceY, sourceZ) = (avatar.X, avatar.Y, avatar.Z);

            // What wired decides is above: whether its movement policy lets the avatar go there.
            // Putting it there is the avatar module's; only the announcing stays here, batched
            // into the action's one packet.
            await AvatarModule.RelocateAvatarAsync(avatar, tileIdx, CancellationToken);

            UserMoves.Add(
                new()
                {
                    ObjectId = avatar.ObjectId,
                    SourceX = sourceX,
                    SourceY = sourceY,
                    SourceZ = sourceZ,
                    TargetX = avatar.X,
                    TargetY = avatar.Y,
                    TargetZ = avatar.Z,
                    MoveType = moveType,
                    AnimationTime = instant ? 0 : GetAnimationTime(),
                    BodyDirection = avatar.Rotation,
                    HeadDirection = avatar.HeadRotation,
                    JumpPower = Policy.JumpStrength ?? avatar.JumpPower,
                }
            );

            return true;
        }
        catch (Exception ex)
        {
            _roomGrain._logger.LogWarning(
                ex,
                "Wired could not move avatar {ObjectId} to tile {TileIdx} in room {RoomId}",
                avatar.ObjectId,
                tileIdx,
                _roomGrain.RoomId
            );

            return false;
        }
    }

    public Task ProcessUserDirectionAsync(IRoomAvatar avatar, Rotation body, Rotation head)
    {
        if (avatar is null)
            return Task.CompletedTask;

        avatar.SetBodyRotation(body);
        avatar.SetHeadRotation(head);
        avatar.MarkDirty();

        UserDirections.Add(
            new()
            {
                ObjectId = avatar.ObjectId,
                BodyRotation = body,
                HeadRotation = head,
            }
        );

        return Task.CompletedTask;
    }

    /// <summary>
    /// The placeholders a signal or stack call from here carries on: what this stack's own read
    /// now, over the ones it was carried.
    /// </summary>
    public async Task<Dictionary<string, string>> ResolvePlaceholdersToCarryAsync(
        CancellationToken ct
    )
    {
        var carried = new Dictionary<string, string>(CarriedPlaceholders, StringComparer.Ordinal);

        foreach (var placeholder in Policy.TextPlaceholders)
        {
            if (placeholder.Token.Length == 0)
                continue;

            try
            {
                carried[placeholder.Token] = await placeholder.ApplyAsync(
                    this,
                    placeholder.Token,
                    ct
                );
            }
            catch (Exception ex)
            {
                _roomGrain._logger.LogWarning(
                    ex,
                    "A wired text placeholder failed in room {RoomId}",
                    _roomGrain.RoomId
                );
            }
        }

        return carried;
    }

    public async Task<string> FormatTextAsync(string text, CancellationToken ct)
    {
        if (
            string.IsNullOrEmpty(text)
            || (Policy.TextPlaceholders.Count == 0 && CarriedPlaceholders.Count == 0)
        )
            return text ?? string.Empty;

        foreach (var placeholder in Policy.TextPlaceholders)
        {
            try
            {
                text = await placeholder.ApplyAsync(this, text, ct);
            }
            catch (Exception ex)
            {
                _roomGrain._logger.LogWarning(
                    ex,
                    "A wired text placeholder failed in room {RoomId}",
                    _roomGrain.RoomId
                );
            }
        }

        foreach (var (token, value) in CarriedPlaceholders)
            text = text.Replace(token, value, StringComparison.Ordinal);

        // Placeholders expand the text, and a box can repeat one many times over.
        // Cut by hand rather than through ClientText.Truncate, which also trims: the text is
        // the box's own, spacing included, and only its length is capped here.
        var maxLength = _roomGrain._wiredConfig.StringParamMaxLength;

        return text.Length > maxLength ? text[..maxLength] : text;
    }

    public ActionContext AsActionContext() => ActionContext.CreateForWired(_roomGrain.RoomId);

    // Wired effects run inside a room tick and carry no token of their own.
    public Task SendComposerToRoomAsync(IComposer composer) =>
        Room.SendComposerToRoomAsync(composer, CancellationToken.None);

    private int GetAnimationTime() =>
        Policy.AnimationMode == WiredAnimationModeType.Instant ? 0 : Policy.AnimationTimeMs;

    /// <summary>Avatars the "carry users" addon drags along with a moving item.</summary>
    private List<IRoomAvatar> CollectCarriedAvatars(IRoomFloorItem floorItem)
    {
        var carried = new List<IRoomAvatar>();

        if (Policy.CarryUsers is not { } carryMode)
            return carried;

        carried.AddRange(
            AvatarModule.GetAvatarsOnItem(
                floorItem,
                standingOnIt: carryMode == WiredCarryUserType.StandingOnFurni
            )
        );

        return carried;
    }
}
