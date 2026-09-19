using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture;
using Turbo.Primitives.Furniture.ExtraData;
using Turbo.Primitives.Furniture.Interactions;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Messages.Outgoing.Room.Furniture;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Enums.Wallet;
using Turbo.Primitives.Players.Wallet;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object.Furniture;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor;

/// <summary>
/// A patch of floor a visitor can rent: for the price and time the hotel sets they may place
/// and move their own furni on the tiles it covers, without having rights in the room
/// (<see cref="IRoomBuildArea"/>). A player rents one space per room. When the rent ends,
/// because its time is up or the renter or the room owner ended it, the renter's furni on
/// those tiles goes back to their inventory. Who rents it, and until when, is kept with the item
/// (<see cref="RentableSpaceData"/>); the state is the look (<see cref="RentableSpaceStates"/>).
/// </summary>
[RoomObjectLogic("rentable_space")]
public class FurnitureRentableSpaceLogic(
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureFloorLogic(stuffDataFactory, ctx), IRoomBuildArea
{
    public override async Task OnAttachAsync(CancellationToken ct)
    {
        await base.OnAttachAsync(ct);

        // A rent that ran out while the room was asleep ends as soon as the room ticks: by then
        // the furni standing on the space has loaded too.
        if (ReadRent() is not null)
            ScheduleExpiry();
    }

    public override Task OnPickupAsync(ActionContext ctx, CancellationToken ct)
    {
        _roomGrain.TimerSystem.Cancel(_ctx.ObjectId);

        return base.OnPickupAsync(ctx, ct);
    }

    public bool GrantsBuildRights(PlayerId playerId, IReadOnlyCollection<int> tileIds) =>
        GetActiveRent() is { } rent
        && rent.RenterId == playerId
        && _roomGrain.FurniModule.GetTileIdForFloorItem(_ctx.RoomObject, out var area)
        && tileIds.All(area.Contains);

    public override async Task<bool> OnInteractAsync(
        ActionContext ctx,
        FurnitureInteraction interaction,
        CancellationToken ct
    )
    {
        switch (interaction)
        {
            case RequestRentableSpaceStatusInteraction:
                await SendStatusAsync(ctx, ct);

                return true;
            case RentSpaceInteraction:
                return await RentAsync(ctx, interaction, ct);
            case CancelSpaceRentInteraction:
                return await CancelAsync(ctx, interaction, ct);
            default:
                return false;
        }
    }

    private async Task<bool> RentAsync(
        ActionContext ctx,
        FurnitureInteraction interaction,
        CancellationToken ct
    )
    {
        var refusal = await GetRentRefusalAsync(ctx, ct);

        if (refusal == RentableSpaceRentFailedType.None && !await TryPayAsync(ctx, ct))
            refusal = RentableSpaceRentFailedType.NotEnoughCredits;

        if (refusal != RentableSpaceRentFailedType.None)
        {
            await SendAsync(
                ctx,
                new RentableSpaceRentFailedMessageComposer { Reason = refusal },
                ct
            );

            return Reject(ctx, interaction, refusal.ToString());
        }

        var renter = await _roomGrain
            ._grainFactory.GetPlayerGrain(ctx.PlayerId)
            .GetSummaryAsync(ct);
        var expiresAt =
            DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            + _roomGrain._roomConfig.RentableSpaceDurationSeconds;

        _ctx.RoomObject.ExtraData.UpdateSection(
            RentableSpaceData.SECTION,
            new RentableSpaceData
            {
                RenterId = ctx.PlayerId,
                RenterName = renter.Name,
                ExpiresAt = expiresAt,
            }
        );

        ScheduleExpiry();

        await SetStateAsync(RentableSpaceStates.RENTED);
        await SendAsync(
            ctx,
            // Unix seconds. The client only takes it as the sign that the rent went through and
            // asks for the status again.
            new RentableSpaceRentOkMessageComposer { ExpiryTime = (int)expiresAt },
            ct
        );
        await SendStatusAsync(ctx, ct);

        return true;
    }

    private async Task<bool> CancelAsync(
        ActionContext ctx,
        FurnitureInteraction interaction,
        CancellationToken ct
    )
    {
        var refusal = GetActiveRent() switch
        {
            null => RentableSpaceRentFailedType.NotRented,
            var rent
                when rent.RenterId != ctx.PlayerId
                    && !await _roomGrain.SecurityModule.GetIsRoomOwnerAsync(ctx) =>
                RentableSpaceRentFailedType.NotRentedByYou,
            _ => RentableSpaceRentFailedType.None,
        };

        if (refusal != RentableSpaceRentFailedType.None)
        {
            await SendAsync(
                ctx,
                new RentableSpaceRentFailedMessageComposer { Reason = refusal },
                ct
            );

            return Reject(ctx, interaction, refusal.ToString());
        }

        await EndRentAsync(ct);
        await SendStatusAsync(ctx, ct);

        return true;
    }

    /// <summary>Why this player cannot rent the space right now, or <see cref="RentableSpaceRentFailedType.None"/>.</summary>
    private async Task<RentableSpaceRentFailedType> GetRentRefusalAsync(
        ActionContext ctx,
        CancellationToken ct
    )
    {
        if (GetActiveRent() is not null)
            return RentableSpaceRentFailedType.AlreadyRented;

        if (RentsAnotherSpace(ctx.PlayerId))
            return RentableSpaceRentFailedType.CanRentOnlyOneSpace;

        var price = _roomGrain._roomConfig.RentableSpacePriceCredits;

        if (price <= 0)
            return RentableSpaceRentFailedType.None;

        var credits = await _roomGrain
            ._grainFactory.GetPlayerWalletGrain(ctx.PlayerId)
            .GetAmountForCurrencyAsync(
                new CurrencyKind { CurrencyType = CurrencyType.Credits },
                ct
            );

        return credits < price
            ? RentableSpaceRentFailedType.NotEnoughCredits
            : RentableSpaceRentFailedType.None;
    }

    private bool RentsAnotherSpace(PlayerId playerId) =>
        _roomGrain.FurniModule.Items.Any(x =>
            x.ObjectId != _ctx.ObjectId
            && x.Logic is FurnitureRentableSpaceLogic other
            && other.GetActiveRent()?.RenterId == playerId
        );

    private async Task<bool> TryPayAsync(ActionContext ctx, CancellationToken ct)
    {
        var price = _roomGrain._roomConfig.RentableSpacePriceCredits;

        if (price <= 0)
            return true;

        var result = await _roomGrain
            ._grainFactory.GetPlayerWalletGrain(ctx.PlayerId)
            .TryDebitAsync(
                [
                    new WalletDebitRequest
                    {
                        CurrencyKind = new CurrencyKind { CurrencyType = CurrencyType.Credits },
                        Amount = price,
                    },
                ],
                ct
            );

        return result.Succeeded;
    }

    /// <summary>Frees the space and sends what the renter built on it back to them.</summary>
    private async Task EndRentAsync(CancellationToken ct)
    {
        var rent = ReadRent();

        _roomGrain.TimerSystem.Cancel(_ctx.ObjectId);
        _ctx.RoomObject.ExtraData.UpdateSection(RentableSpaceData.SECTION, new RentableSpaceData());

        await SetStateAsync(RentableSpaceStates.FREE);

        if (
            rent is not { RenterId: > 0 }
            || !_roomGrain.FurniModule.GetTileIdForFloorItem(_ctx.RoomObject, out var area)
        )
            return;

        var built = area.SelectMany(_roomGrain.FurniModule.GetFloorItemsOnTile)
            .Where(x => x.ObjectId != _ctx.ObjectId && x.OwnerId == rent.RenterId)
            .DistinctBy(x => x.ObjectId)
            .Cast<IRoomItem>()
            .ToList();

        await _roomGrain.ActionModule.ReturnItemsToOwnersAsync(built, ct);
    }

    private void ScheduleExpiry()
    {
        if (ReadRent() is not { RenterId: > 0 } rent)
            return;

        var remaining = DateTimeOffset.FromUnixTimeSeconds(rent.ExpiresAt) - DateTimeOffset.UtcNow;

        _roomGrain.TimerSystem.Schedule(
            _ctx.ObjectId,
            (int)Math.Clamp(remaining.TotalMilliseconds, 1, int.MaxValue),
            EndRentAsync
        );
    }

    private async Task SendStatusAsync(ActionContext ctx, CancellationToken ct)
    {
        var rent = GetActiveRent();

        await SendAsync(
            ctx,
            new RentableSpaceStatusMessageComposer
            {
                Rented = rent is not null,
                CanRentErrorCode = await GetRentRefusalAsync(ctx, ct),
                RenterId = rent?.RenterId ?? -1,
                RenterName = rent?.RenterName ?? string.Empty,
                TimeRemaining = rent is null
                    ? 0
                    : (int)Math.Max(0, rent.ExpiresAt - DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
                Price = _roomGrain._roomConfig.RentableSpacePriceCredits,
            },
            ct
        );
    }

    private Task SendAsync(ActionContext ctx, IComposer composer, CancellationToken ct) =>
        _roomGrain._grainFactory.SendComposerToPlayerAsync(ctx.PlayerId, composer, ct);

    /// <summary>The rent while it lasts; a rent whose time is up reads as none even before it is cleared.</summary>
    private RentableSpaceData? GetActiveRent() =>
        ReadRent() is { RenterId: > 0 } rent
        && rent.ExpiresAt > DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            ? rent
            : null;

    private RentableSpaceData? ReadRent() =>
        FurnitureExtraDataSections.Read<RentableSpaceData>(
            _ctx.RoomObject.ExtraData,
            RentableSpaceData.SECTION,
            _roomGrain._logger
        );
}
