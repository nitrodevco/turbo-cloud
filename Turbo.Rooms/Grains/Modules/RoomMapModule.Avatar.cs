using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Logging;
using Turbo.Primitives;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object.Avatars;

namespace Turbo.Rooms.Grains.Modules;

internal sealed partial class RoomMapModule
{
    public async Task InvokeAvatarAsync(IRoomAvatar avatar, CancellationToken ct)
    {
        try
        {
            var tileId = ToIdx(avatar.X, avatar.Y);
            var highestItemId = _state.TileHighestFloorItems[tileId];
            var canSit = false;
            var canLay = false;

            if (_state.FloorItemsById.TryGetValue(highestItemId, out var floorItem))
            {
                canSit = floorItem.Logic.CanSit();
                canLay = floorItem.Logic.CanLay();

                if (canSit)
                    avatar.Sit(true, floorItem.Definition.StackHeight, floorItem.Rotation);
                else if (canLay)
                    avatar.Lay(true, floorItem.Definition.StackHeight, floorItem.Rotation);

                await floorItem.Logic.OnInvokeAsync(avatar.Logic.Context, ct);
            }

            if (!canSit && avatar.HasStatus(AvatarStatusType.Sit))
                avatar.Sit(false);

            if (!canLay && avatar.HasStatus(AvatarStatusType.Lay))
                avatar.Lay(false);

            UpdateHeightForAvatar(avatar);
        }
        catch (Exception) { }
    }

    public async Task InvokeAvatarsOnTilesAsync(List<int> tileIdx, CancellationToken ct)
    {
        if (tileIdx.Count == 0)
            return;

        var avatarIds = new List<long>();

        foreach (var idx in tileIdx)
        {
            try
            {
                if (!InBounds(idx))
                    throw new TurboException(TurboErrorCodeEnum.TileOutOfBounds);

                var avatarStack = _state.TileAvatarStacks[idx];

                foreach (var avatarId in avatarStack)
                    avatarIds.Add(avatarId);
            }
            catch (Exception)
            {
                continue;
            }
        }

        avatarIds = [.. avatarIds.Distinct()];

        foreach (var avatarId in avatarIds)
        {
            try
            {
                if (!_state.AvatarsByObjectId.TryGetValue((int)avatarId, out var avatar))
                    continue;

                await InvokeAvatarAsync(avatar, ct);
            }
            catch (Exception)
            {
                continue;
            }
        }
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

        var tileFlags = _state.TileFlags[tileIdx];

        if (tileFlags.Has(RoomTileFlags.Disabled) || tileFlags.Has(RoomTileFlags.Closed))
            return false;

        var avatarStack = _state.TileAvatarStacks[tileIdx];

        if (avatarStack.Count > 0)
        {
            if (avatarStack.Contains(avatar.ObjectId.Value))
                return true;

            if (isGoal || _state.RoomSnapshot.AllowBlocking)
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

        if (_roomConfig.EnableDiagonalChecking && IsDiagonal(pTileIdx, nTileIdx))
        {
            var left = CanAvatarWalk(avatar, ToIdx(toX, fromY), true, true);
            var right = CanAvatarWalk(avatar, ToIdx(fromX, toY), true, true);

            if (!left && !right)
                return false;
        }

        return true;
    }

    public void UpdateHeightForAvatar(IRoomAvatar avatar)
    {
        var tileId = ToIdx(avatar.X, avatar.Y);
        var next = GetTileHeightForAvatar(tileId);

        avatar.SetHeight(next);
    }

    public double GetTileHeightForAvatar(int tileId)
    {
        try
        {
            var height = _state.TileHeights[tileId];
            var flags = _state.TileFlags[tileId];
            var highestItemId = _state.TileHighestFloorItems[tileId];

            if (
                highestItemId > 0
                && (flags.Has(RoomTileFlags.Sittable) || flags.Has(RoomTileFlags.Layable))
            )
            {
                var floorItem = _state.FloorItemsById[highestItemId];

                if (floorItem is not null)
                    height -= floorItem.Definition.StackHeight;
            }

            return height;
        }
        catch (Exception)
        {
            return 0.0;
        }
    }
}
