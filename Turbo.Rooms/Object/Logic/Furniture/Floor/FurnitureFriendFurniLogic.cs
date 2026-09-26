using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture;
using Turbo.Primitives.Furniture.Interactions;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Furniture.StuffData;
using Turbo.Primitives.Messages.Outgoing.Friendfurni;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor;

/// <summary>
/// A love lock. Unlocked, a double-click by one of two adjacent players starts a confirmation
/// both must answer within the configured time; when both agree the lock's string data is
/// written with the two names, figures and the date, and it stays locked. Locked, the client
/// opens the engraving on its own.
/// </summary>
[RoomObjectLogic("love_lock")]
public class FurnitureFriendFurniLogic(
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureFloorLogic(stuffDataFactory, ctx)
{
    protected override StuffDataType _stuffDataType => StuffDataType.StringKey;

    public override FurnitureUsageType GetUsagePolicy() => FurnitureUsageType.Everybody;

    private bool IsLocked =>
        StuffData is IStringStuffData strings
        && strings.ValueAt(FriendFurniData.STATE_INDEX) == FriendFurniData.LOCKED;

    public override async Task OnUseAsync(ActionContext ctx, int param, CancellationToken ct)
    {
        if (IsLocked || !IsAvatarAdjacent(ctx))
            return;

        var pending = _roomGrain._state.PendingFriendFurniLocks;

        if (pending.ContainsKey(_ctx.ObjectId))
            return;

        var partner = AdjacentPartner(ctx.PlayerId);

        if (partner is null)
            return;

        pending[_ctx.ObjectId] = new FriendFurniLockRequest
        {
            InitiatorId = ctx.PlayerId,
            PartnerId = partner.Value,
        };

        await _roomGrain._grainFactory.SendComposerToPlayerAsync(
            ctx.PlayerId,
            new FriendFurniStartConfirmationMessageComposer
            {
                ItemId = _ctx.ObjectId,
                IsOwner = true,
            },
            ct
        );
        await _roomGrain._grainFactory.SendComposerToPlayerAsync(
            partner.Value,
            new FriendFurniStartConfirmationMessageComposer
            {
                ItemId = _ctx.ObjectId,
                IsOwner = false,
            },
            ct
        );

        _roomGrain.TimerSystem.Schedule(
            _ctx.ObjectId,
            _roomGrain._roomConfig.FriendFurniLockTimeoutMs,
            CancelAsync
        );
    }

    public override async Task<bool> OnInteractAsync(
        ActionContext ctx,
        FurnitureInteraction interaction,
        CancellationToken ct
    )
    {
        if (interaction is not ConfirmFriendFurniLockInteraction confirm)
            return false;

        if (
            !_roomGrain._state.PendingFriendFurniLocks.TryGetValue(_ctx.ObjectId, out var request)
            || !request.Involves(ctx.PlayerId)
        )
            return Reject(ctx, interaction, "no pending lock for this player");

        if (!confirm.Confirmed)
        {
            await CancelAsync(ct);

            return true;
        }

        if (ctx.PlayerId == request.InitiatorId)
            request.InitiatorConfirmed = true;
        else
            request.PartnerConfirmed = true;

        if (!(request.InitiatorConfirmed && request.PartnerConfirmed))
        {
            await _roomGrain._grainFactory.SendComposerToPlayerAsync(
                request.OtherOf(ctx.PlayerId),
                new FriendFurniOtherLockConfirmedMessageComposer { ItemId = _ctx.ObjectId },
                ct
            );

            return true;
        }

        await LockAsync(request, ct);

        return true;
    }

    public override Task OnPickupAsync(ActionContext ctx, CancellationToken ct)
    {
        _roomGrain.TimerSystem.Cancel(_ctx.ObjectId);
        _roomGrain._state.PendingFriendFurniLocks.Remove(_ctx.ObjectId);

        return base.OnPickupAsync(ctx, ct);
    }

    private async Task LockAsync(FriendFurniLockRequest request, CancellationToken ct)
    {
        _roomGrain.TimerSystem.Cancel(_ctx.ObjectId);
        _roomGrain._state.PendingFriendFurniLocks.Remove(_ctx.ObjectId);

        var left = await _roomGrain
            ._grainFactory.GetPlayerGrain(request.InitiatorId)
            .GetSummaryAsync(ct);
        var right = await _roomGrain
            ._grainFactory.GetPlayerGrain(request.PartnerId)
            .GetSummaryAsync(ct);
        var date = DateTime.UtcNow.ToString(
            FriendFurniData.DATE_FORMAT,
            CultureInfo.InvariantCulture
        );

        await SetStringDataAsync([
            FriendFurniData.LOCKED,
            left.Name,
            right.Name,
            left.Figure,
            right.Figure,
            date,
        ]);
    }

    private async Task CancelAsync(CancellationToken ct)
    {
        _roomGrain.TimerSystem.Cancel(_ctx.ObjectId);

        if (!_roomGrain._state.PendingFriendFurniLocks.Remove(_ctx.ObjectId, out var request))
            return;

        var cancel = new FriendFurniCancelLockMessageComposer { ItemId = _ctx.ObjectId };

        await _roomGrain._grainFactory.SendComposerToPlayerAsync(request.InitiatorId, cancel, ct);
        await _roomGrain._grainFactory.SendComposerToPlayerAsync(request.PartnerId, cancel, ct);
    }

    /// <summary>Another player's avatar standing on or next to the lock, if exactly one is there.</summary>
    private PlayerId? AdjacentPartner(PlayerId initiatorId)
    {
        var footprint = FloorFootprint.Of(_ctx.RoomObject);

        var partners = _roomGrain
            .AvatarModule.Players.Where(player =>
                player.PlayerId != initiatorId && footprint.IsOnOrNextTo(player.X, player.Y)
            )
            .Select(player => player.PlayerId)
            .Take(2)
            .ToList();

        return partners.Count == 1 ? partners[0] : null;
    }
}
