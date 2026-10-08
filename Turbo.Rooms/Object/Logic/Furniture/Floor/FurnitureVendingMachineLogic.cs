using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.ExtraData;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor;

/// <summary>
/// A furni that hands out hand items: fridges, drinks machines and ice cream machines (Sulake's
/// <c>VendingMachineFurni</c> and <c>IceCreamMachineFurni</c>), and the static ones that hand
/// something over without a machine (<c>HandItemProviderFurni</c>). What it gives is its
/// definition's <see cref="VendingMachineData"/>; who may use it is the definition's usage
/// policy, which Sulake's data makes everyone's for nearly all of them.
/// <para>
/// A use walks the avatar to the tile in front of it - or, when that tile cannot be reached, the
/// nearest free tile beside it - and once the avatar is there it turns to face the furni, which
/// shows its dispensing state for <c>VendingDispenseMs</c> (when its asset animates one), and
/// the avatar is handed one of its items at random. A use from where it would be served is
/// served at once. Anyone who walks off somewhere else on the way, or takes too long, is not
/// served.
/// </para>
/// </summary>
[RoomObjectLogic("vending_machine")]
public class FurnitureVendingMachineLogic : FurnitureFloorLogic
{
    private const int IDLE_STATE = 0;
    private const int DISPENSING_STATE = 1;

    private readonly VendingMachineData _data;

    /// <summary>Avatars on their way to be served: the tile they were sent to, and until when.</summary>
    private readonly Dictionary<RoomObjectId, (int TileIdx, long UntilMs)> _approaching = [];

    /// <summary>When the dispensing state goes back to idle; 0 when it is not dispensing.</summary>
    private long _dispensingUntilMs;

    public FurnitureVendingMachineLogic(
        IStuffDataFactory stuffDataFactory,
        IRoomFloorItemContext ctx
    )
        : base(stuffDataFactory, ctx)
    {
        _data =
            FurnitureExtraDataSections.Read<VendingMachineData>(
                ctx.RoomObject.ExtraData,
                ctx.Definition.ExtraData,
                VendingMachineData.SECTION,
                _roomGrain._logger
            ) ?? new VendingMachineData();
    }

    public override async Task OnUseAsync(ActionContext ctx, int param, CancellationToken ct)
    {
        if (_data.HandItems.IsDefaultOrEmpty || GetAvatar(ctx) is not { } avatar)
            return;

        var map = MapModule;
        var avatarIdx = map.ToIdx(avatar.X, avatar.Y);
        var serveIdx = ServingTiles(avatar).FirstOrDefault(-1);

        if (serveIdx < 0)
            return;

        if (avatarIdx == serveIdx && !avatar.IsWalking)
        {
            await ServeAsync(avatar, ct);

            return;
        }

        foreach (var tileIdx in ServingTiles(avatar))
        {
            var (x, y) = map.GetTileXY(tileIdx);

            if (!await AvatarModule.WalkAvatarToAsync(avatar, x, y, ct))
                continue;

            _approaching[avatar.ObjectId] = (
                tileIdx,
                _roomGrain.NowMs() + _roomGrain._roomConfig.VendingWalkTimeoutMs
            );
            ScheduleTick();

            return;
        }
    }

    public override Task OnPickupAsync(ActionContext ctx, CancellationToken ct)
    {
        TimerSystem.Cancel(_ctx.ObjectId);
        _approaching.Clear();
        _dispensingUntilMs = 0;

        return base.OnPickupAsync(ctx, ct);
    }

    /// <summary>
    /// Where an avatar is served from, best first: the tile in front of the furni, then the free
    /// tiles beside it nearest to the avatar. Tiles off the map or under other furni nobody may
    /// stand on are left out; a path is not asked for until the avatar is sent.
    /// </summary>
    private IEnumerable<int> ServingTiles(IRoomAvatar avatar)
    {
        var map = MapModule;
        var footprint = FloorFootprint.Of(_ctx.RoomObject);

        if (
            map.TryGetTileInFront(_ctx.GetTileIdx(), _ctx.RoomObject.Rotation, out var frontIdx)
            && map.CanAvatarWalk(avatar, frontIdx)
        )
            yield return frontIdx;

        var beside = footprint
            .Tiles()
            .SelectMany(tile =>
                from dx in new[] { -1, 0, 1 }
                from dy in new[] { -1, 0, 1 }
                select (X: tile.X + dx, Y: tile.Y + dy)
            )
            .Distinct()
            .Where(tile =>
                map.InBounds(tile.X, tile.Y) && footprint.DistanceTo(tile.X, tile.Y) == 1
            )
            .Select(tile => (Idx: map.ToIdx(tile.X, tile.Y), tile.X, tile.Y))
            .Where(tile => tile.Idx != frontIdx && map.CanAvatarWalk(avatar, tile.Idx))
            .OrderBy(tile =>
                (tile.X - avatar.X) * (tile.X - avatar.X)
                + (tile.Y - avatar.Y) * (tile.Y - avatar.Y)
            );

        // An avatar already beside it is served where it stands.
        if (footprint.DistanceTo(avatar.X, avatar.Y) == 1 && !avatar.IsWalking)
            yield return map.ToIdx(avatar.X, avatar.Y);

        foreach (var tile in beside)
            yield return tile.Idx;
    }

    /// <summary>Faces the furni, shows it dispensing, and hands over one of its items.</summary>
    private async Task ServeAsync(IRoomAvatar avatar, CancellationToken ct)
    {
        var rotation = RotationExtensions.FromPoints(
            avatar.X,
            avatar.Y,
            _ctx.RoomObject.X,
            _ctx.RoomObject.Y
        );

        avatar.SetBodyRotation(rotation);
        avatar.SetHeadRotation(rotation);
        avatar.MarkDirty();

        if (_data.Animates)
        {
            if (GetState() != DISPENSING_STATE)
                await SetStateAsync(DISPENSING_STATE);

            _dispensingUntilMs = _roomGrain.NowMs() + _roomGrain._roomConfig.VendingDispenseMs;
            ScheduleTick();
        }

        var handItem = _data.HandItems[Random.Shared.Next(_data.HandItems.Length)];

        await AvatarModule.SetHandItemAsync(avatar, handItem, ct);
    }

    private void ScheduleTick() =>
        TimerSystem.Schedule(_ctx.ObjectId, _roomGrain._roomConfig.AvatarTickMs, TickAsync);

    /// <summary>
    /// Serves whoever has arrived, forgets whoever went elsewhere or took too long, and puts the
    /// furni back to idle once its dispensing time is up; runs while any of that is pending.
    /// </summary>
    private async Task TickAsync(CancellationToken ct)
    {
        var now = _roomGrain.NowMs();

        foreach (var (objectId, (tileIdx, untilMs)) in _approaching.ToArray())
        {
            if (!AvatarModule.TryGetAvatar(objectId, out var avatar))
            {
                _approaching.Remove(objectId);

                continue;
            }

            var arrived = MapModule.ToIdx(avatar.X, avatar.Y) == tileIdx && !avatar.IsWalking;

            if (arrived)
            {
                _approaching.Remove(objectId);

                await ServeAsync(avatar, ct);
            }
            else if (!avatar.IsWalking || now >= untilMs || avatar.GoalTileId != tileIdx)
                _approaching.Remove(objectId);
        }

        if (_dispensingUntilMs > 0 && now >= _dispensingUntilMs)
        {
            _dispensingUntilMs = 0;

            if (GetState() != IDLE_STATE)
                await SetStateAsync(IDLE_STATE);
        }

        if (_approaching.Count > 0 || _dispensingUntilMs > 0)
            ScheduleTick();
    }
}
