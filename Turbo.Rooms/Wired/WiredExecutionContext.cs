using System;
using System.Collections.Generic;
using System.Linq;
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
    public DateTimeOffset RoomLocalTime => _roomGrain.WiredSystem.GetRoomLocalTime();

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
            var sourceIdx = _roomGrain.MapModule.ToIdx(sourceX, sourceY);
            var carried = CollectCarriedAvatars(floorItem);

            if (Policy.MovePhysics.HasFlag(WiredMovePhysicsFlags.KeepAltitude))
                z ??= floorItem.Z;

            // Through the furni module, so the item's logic hears of the move as it would from a
            // player; only the announcing stays here, batched into the action's one packet.
            if (
                !await _roomGrain.FurniModule.MoveFloorItemAsync(
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

                    if (!_roomGrain.MapModule.InBounds(targetX, targetY))
                        continue;

                    await ProcessUserMovementAsync(
                        avatar,
                        _roomGrain.MapModule.ToIdx(targetX, targetY),
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
                await _roomGrain.FurniModule.MoveWallItemAsync(
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

    public async Task<bool> ProcessUserMovementAsync(
        IRoomAvatar avatar,
        int tileIdx,
        SlideAvatarMoveType moveType
    )
    {
        if (avatar is null)
            return false;

        try
        {
            var map = _roomGrain.MapModule;

            if (!map.InBounds(tileIdx))
                return false;

            var sourceIdx = map.ToIdx(avatar.X, avatar.Y);

            if (sourceIdx == tileIdx)
                return true;

            if (
                !Policy.MovePhysics.HasFlag(WiredMovePhysicsFlags.MoveThroughUsers)
                && !map.CanAvatarWalk(avatar, tileIdx, true)
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
            await _roomGrain.AvatarModule.RelocateAvatarAsync(avatar, tileIdx, CancellationToken);

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
                    AnimationTime = GetAnimationTime(),
                    BodyDirection = avatar.Rotation,
                    HeadDirection = avatar.HeadRotation,
                    JumpPower = avatar.JumpPower,
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

    public async Task<string> FormatTextAsync(string text, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(text) || Policy.TextPlaceholders.Count == 0)
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

        // Placeholders expand the text, and a box can repeat one many times over.
        var maxLength = _roomGrain._wiredConfig.StringParamMaxLength;

        if (text.Length > maxLength)
            text = text[..maxLength];

        return text;
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
            _roomGrain.AvatarModule.GetAvatarsOnItem(
                floorItem,
                standingOnIt: carryMode == WiredCarryUserType.StandingOnFurni
            )
        );

        return carried;
    }
}
