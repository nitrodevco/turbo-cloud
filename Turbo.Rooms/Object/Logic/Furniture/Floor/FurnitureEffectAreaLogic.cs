using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor;

/// <summary>
/// Furni that puts an avatar effect on whoever stands on it and takes it off when they step off
/// (Sulake's <c>AvatarEffectAreaFurniture</c>: pools and hot tubs swim, 29; dance floors, 140;
/// trampolines and bouncy castles, 193; the Builders Club water block). The effect is its
/// definition's <c>customparams</c> and is the hotel's, not one the player owns.
/// <para>
/// Stepping from one tile of it onto another, or onto other furni with the same effect, keeps the
/// effect on: a step tells the tile left before the tile landed on, so the effect is only taken
/// off once the step is settled and the avatar stands on nothing that gives it. An avatar that
/// put on something else meanwhile keeps it.
/// </para>
/// </summary>
[RoomObjectLogic("effect_area")]
public class FurnitureEffectAreaLogic(IStuffDataFactory stuffDataFactory, IRoomFloorItemContext ctx)
    : FurnitureFloorLogic(stuffDataFactory, ctx)
{
    private readonly int? _effectId = AvatarEffectFurni.EffectIdOf(ctx.Definition);

    /// <summary>Avatars that stepped off it, settled on the next timer run.</summary>
    private readonly HashSet<RoomObjectId> _leaving = [];

    /// <summary>Whether the effect stays on when the avatar steps off.</summary>
    protected virtual bool KeepsEffectOnLeave => false;

    /// <summary>Whether anyone stands on it, or is stepping onto it.</summary>
    protected bool IsOccupied => AvatarsOnIt().Any();

    public override async Task OnWalkOnAsync(IRoomAvatarContext ctx, CancellationToken ct)
    {
        await base.OnWalkOnAsync(ctx, ct);

        if (_effectId is not { } effectId || ctx.RoomObject is not IRoomAvatar avatar)
            return;

        _leaving.Remove(avatar.ObjectId);

        if (avatar.EffectId != effectId)
            await AvatarModule.SetAvatarEffectAsync(avatar.ObjectId, effectId, ct);

        await OnOccupancyChangedAsync(occupied: true, ct);
    }

    public override async Task OnWalkOffAsync(IRoomAvatarContext ctx, CancellationToken ct)
    {
        await base.OnWalkOffAsync(ctx, ct);

        if (_effectId is null || ctx.RoomObject is not IRoomAvatar avatar)
            return;

        if (!KeepsEffectOnLeave)
            _leaving.Add(avatar.ObjectId);

        TimerSystem.Schedule(_ctx.ObjectId, 0, SettleAsync);
    }

    public override async Task OnPickupAsync(ActionContext ctx, CancellationToken ct)
    {
        TimerSystem.Cancel(_ctx.ObjectId);
        _leaving.Clear();

        IRoomAvatar[] standing = KeepsEffectOnLeave ? [] : [.. AvatarsOnIt()];

        await base.OnPickupAsync(ctx, ct);

        foreach (var avatar in standing)
            if (_effectId > 0 && avatar.EffectId == _effectId)
                await AvatarModule.SetAvatarEffectAsync(avatar.ObjectId, 0, ct);
    }

    /// <summary>
    /// Called when an avatar steps onto it (<paramref name="occupied"/> true; the step is not
    /// over, so the avatar is not yet counted on it) and when a step off it is settled.
    /// </summary>
    protected virtual Task OnOccupancyChangedAsync(bool occupied, CancellationToken ct) =>
        Task.CompletedTask;

    private IEnumerable<IRoomAvatar> AvatarsOnIt()
    {
        var footprint = FloorFootprint.Of(_ctx.RoomObject);

        return AvatarModule.Avatars.Where(avatar =>
        {
            var (x, y) = MapModule.GetTileXY(TileOf(avatar));

            return footprint.DistanceTo(x, y) == 0;
        });
    }

    private async Task SettleAsync(CancellationToken ct)
    {
        foreach (var objectId in _leaving.ToArray())
        {
            _leaving.Remove(objectId);

            if (
                _effectId is not > 0
                || !AvatarModule.TryGetAvatar(objectId, out var avatar)
                || avatar.EffectId != _effectId
                || GivesSameEffect(TileOf(avatar))
            )
                continue;

            await AvatarModule.SetAvatarEffectAsync(objectId, 0, ct);
        }

        await OnOccupancyChangedAsync(IsOccupied, ct);
    }

    private bool GivesSameEffect(int tileIdx) =>
        MapModule.TryGetHighestFloorItem(tileIdx, out var item)
        && item.Logic is FurnitureEffectAreaLogic area
        && area._effectId == _effectId;

    // Mid-step an avatar is already on the tile it steps onto, though its position is not.
    private int TileOf(IRoomAvatar avatar) =>
        avatar.NextTileId >= 0 ? avatar.NextTileId : MapModule.ToIdx(avatar.X, avatar.Y);
}
