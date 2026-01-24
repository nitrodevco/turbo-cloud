using System;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Logging;
using Turbo.Primitives;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;

namespace Turbo.Rooms.Grains.Modules;

public sealed partial class RoomMapModule
{
    public async Task InvokeAvatarAsync(IRoomAvatar avatar, CancellationToken ct)
    {
        try
        {
            avatar.NeedsInvoke = false;

            if (avatar.IsWalking)
                return;

            var tileId = ToIdx(avatar.X, avatar.Y);
            var highestItemId = _roomGrain._state.TileHighestFloorItems[tileId];
            var canSit = false;
            var canLay = false;

            if (
                _roomGrain._state.ItemsById.TryGetValue(highestItemId, out var item)
                && item is IRoomFloorItem floorItem
            )
            {
                canSit = floorItem.Logic.CanSit();
                canLay = floorItem.Logic.CanLay();

                if (canSit)
                    avatar.Sit(true, floorItem.Logic.GetPostureOffset(), floorItem.Rotation);
                else if (canLay)
                    avatar.Lay(true, floorItem.Logic.GetPostureOffset(), floorItem.Rotation);

                await floorItem.Logic.OnInvokeAsync((IRoomAvatarContext)avatar.Logic.Context, ct);
            }

            if (!canSit && avatar.HasStatus(AvatarStatusType.Sit))
                avatar.Sit(false);

            if (!canLay && avatar.HasStatus(AvatarStatusType.Lay))
                avatar.Lay(false);

            UpdateHeightForAvatar(avatar);
        }
        catch (Exception) { }
    }

    public bool CanAvatarWalk(
        IRoomAvatar avatar,
        int tileIdx,
        bool isGoal = true,
        bool isDiagonalCheck = false
    )
    {
        if (!InBounds(tileIdx))
            return false;

        var tileFlags = _roomGrain._state.TileFlags[tileIdx];

        if (tileFlags.Has(RoomTileFlags.Disabled) || tileFlags.Has(RoomTileFlags.Closed))
            return false;

        if (tileFlags.Has(RoomTileFlags.AvatarOccupied))
        {
            if (_roomGrain._state.TileAvatarStacks[tileIdx].Contains(avatar.ObjectId))
                return true;

            if (isGoal || _roomGrain._state.RoomSnapshot.AllowBlocking)
                return false;
        }

        if (
            (tileFlags.Has(RoomTileFlags.Sittable) || tileFlags.Has(RoomTileFlags.Layable))
            && (isDiagonalCheck || !isGoal)
        )
            return false;

        return true;
    }

    public bool CanAvatarWalkBetween(
        IRoomAvatar avatar,
        int pTileIdx,
        int nTileIdx,
        bool isGoal = true
    )
    {
        if (!CanAvatarWalk(avatar, nTileIdx, isGoal))
            return false;

        var (fromX, fromY) = GetTileXY(pTileIdx);
        var (toX, toY) = GetTileXY(nTileIdx);

        if (_roomGrain._roomConfig.EnableDiagonalChecking && IsDiagonal(pTileIdx, nTileIdx))
        {
            var left = CanAvatarWalk(avatar, ToIdx(toX, fromY), true, true);
            var right = CanAvatarWalk(avatar, ToIdx(fromX, toY), true, true);

            if (!left && !right)
                return false;
        }

        return true;
    }

    public bool RollAvatar(IRoomAvatar avatar, int tileIdx, Altitude z)
    {
        if (!InBounds(tileIdx))
            throw new TurboException(TurboErrorCodeEnum.TileOutOfBounds);

        RemoveAvatar(avatar, false);

        avatar.SetPosition(GetX(tileIdx), GetY(tileIdx));

        AddAvatar(avatar, false);

        avatar.SetHeight(z);

        return true;
    }

    public void AddAvatar(IRoomAvatar avatar, bool flush)
    {
        var tileIdx = ToIdx(avatar.X, avatar.Y);

        AddAvatarAtIdx(avatar, tileIdx, flush);
    }

    public void AddAvatarAtIdx(IRoomAvatar avatar, int tileIdx, bool flush)
    {
        if (!InBounds(tileIdx))
            throw new TurboException(TurboErrorCodeEnum.TileOutOfBounds);

        _roomGrain._state.TileAvatarStacks[tileIdx].Add(avatar.ObjectId);

        ComputeTile(tileIdx);

        if (flush) { }
    }

    public void RemoveAvatar(IRoomAvatar avatar, bool flush)
    {
        var tileIdx = ToIdx(avatar.X, avatar.Y);

        RemoveAvatarAtIdx(avatar, tileIdx, flush);
    }

    public void RemoveAvatarAtIdx(IRoomAvatar avatar, int tileIdx, bool flush)
    {
        if (!InBounds(tileIdx))
            throw new TurboException(TurboErrorCodeEnum.TileOutOfBounds);

        _roomGrain._state.TileAvatarStacks[tileIdx].Remove(avatar.ObjectId);

        ComputeTile(tileIdx);

        if (flush) { }
    }

    public void UpdateHeightForAvatar(IRoomAvatar avatar)
    {
        try
        {
            var tileId = ToIdx(avatar.X, avatar.Y);
            var height = _roomGrain._state.TileHeights[tileId];
            var highestItemId = _roomGrain._state.TileHighestFloorItems[tileId];
            var postureOffset = Altitude.Zero;

            if (highestItemId > 0)
            {
                if (
                    _roomGrain._state.ItemsById.TryGetValue(highestItemId, out var item)
                    && item is IRoomFloorItem floorItem
                )
                {
                    postureOffset = floorItem.Logic.GetPostureOffset();
                }
            }

            avatar.PostureOffset = postureOffset;

            avatar.SetHeight(height - postureOffset);
        }
        catch (Exception) { }
    }

    public Altitude GetTileHeightForAvatar(int tileId)
    {
        try
        {
            var height = _roomGrain._state.TileHeights[tileId];
            var highestItemId = _roomGrain._state.TileHighestFloorItems[tileId];
            var postureOffset = Altitude.Zero;

            if (highestItemId > 0)
            {
                if (
                    _roomGrain._state.ItemsById.TryGetValue(highestItemId, out var item)
                    && item is IRoomFloorItem floorItem
                )
                {
                    postureOffset = floorItem.Logic.GetPostureOffset();
                }
            }

            return height - postureOffset;
        }
        catch (Exception)
        {
            return Altitude.Zero;
        }
    }
}
