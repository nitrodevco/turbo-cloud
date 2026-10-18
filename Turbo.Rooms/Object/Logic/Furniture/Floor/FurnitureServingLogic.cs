using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor;

/// <summary>
/// A furni that serves whoever uses it: a vending machine handing out a drink, or a furni handing
/// out an avatar effect. Who may use it is the definition's usage policy.
/// <para>
/// A use walks the avatar to the tile in front of it - or, when that tile cannot be reached, the
/// nearest free tile beside it - and once the avatar is there it turns to face the furni, which
/// shows its dispensing state for <c>VendingDispenseMs</c> (when its asset animates one), and
/// the avatar is handed what it serves. A use from where it would be served is
/// served at once. Anyone who walks off somewhere else on the way, or takes too long, is not
/// served.
/// </para>
/// </summary>
public abstract class FurnitureServingLogic : FurnitureWalkToUseLogic
{
    private const int IDLE_STATE = 0;
    private const int DISPENSING_STATE = 1;

    /// <summary>When the dispensing state goes back to idle; 0 when it is not dispensing.</summary>
    private long _dispensingUntilMs;

    protected FurnitureServingLogic(IStuffDataFactory stuffDataFactory, IRoomFloorItemContext ctx)
        : base(stuffDataFactory, ctx) { }

    /// <summary>Whether it has anything to serve; a use of one that has not is ignored.</summary>
    protected abstract bool CanServe { get; }

    /// <summary>Whether it shows its dispensing state (state 1) while it serves.</summary>
    protected abstract bool Animates { get; }

    /// <summary>Hands the avatar, which stands beside it facing it, what it serves.</summary>
    protected abstract Task HandOverAsync(IRoomAvatar avatar, CancellationToken ct);

    protected override int WalkTimeoutMs => _roomGrain._roomConfig.VendingWalkTimeoutMs;

    public override async Task OnUseAsync(ActionContext ctx, int param, CancellationToken ct)
    {
        if (!CanServe || GetAvatar(ctx) is not { } avatar)
            return;

        await WalkToUseAsync(ctx, avatar, ServingTiles(avatar), ct);
    }

    public override Task OnPickupAsync(ActionContext ctx, CancellationToken ct)
    {
        _dispensingUntilMs = 0;

        return base.OnPickupAsync(ctx, ct);
    }

    protected override Task UseFromTileAsync(
        ActionContext ctx,
        IRoomAvatar avatar,
        CancellationToken ct
    ) => ServeAsync(avatar, ct);

    /// <summary>
    /// Where an avatar is served from, best first: the tile in front of the furni, then the free
    /// tiles beside it nearest to the avatar.
    /// </summary>
    private IEnumerable<int> ServingTiles(IRoomAvatar avatar)
    {
        var map = MapModule;

        if (
            map.TryGetTileInFront(_ctx.GetTileIdx(), _ctx.RoomObject.Rotation, out var frontIdx)
            && map.CanAvatarWalk(avatar, frontIdx)
        )
            yield return frontIdx;

        // An avatar already beside it is served where it stands.
        if (StandsBeside(avatar))
            yield return map.ToIdx(avatar.X, avatar.Y);

        foreach (var tileIdx in TilesBeside(avatar).Where(tileIdx => tileIdx != frontIdx))
            yield return tileIdx;
    }

    /// <summary>Faces the furni, shows it dispensing, and hands over what it serves.</summary>
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

        if (Animates)
        {
            if (GetState() != DISPENSING_STATE)
                await SetStateAsync(DISPENSING_STATE);

            _dispensingUntilMs = _roomGrain.NowMs() + _roomGrain._roomConfig.VendingDispenseMs;
            ScheduleTick();
        }

        await HandOverAsync(avatar, ct);
    }

    /// <summary>Puts the furni back to idle once its dispensing time is up.</summary>
    protected override async Task<bool> TickMoreAsync(long nowMs, CancellationToken ct)
    {
        if (_dispensingUntilMs > 0 && nowMs >= _dispensingUntilMs)
        {
            _dispensingUntilMs = 0;

            if (GetState() != IDLE_STATE)
                await SetStateAsync(IDLE_STATE);
        }

        return _dispensingUntilMs > 0;
    }
}
