using System.Collections.Immutable;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Snapshots;

namespace Turbo.Rooms.Grains.Modules;

/// <summary>
/// The room's share of trading: who the clicked avatar is, whether the room's trade mode lets
/// each side trade, and the trading status on the avatars. The trades themselves live in
/// <see cref="RoomTradeGrain"/>, which calls in here; nothing here calls it back awaited.
/// </summary>
public sealed class RoomTradeModule(RoomGrain roomGrain)
{
    private readonly RoomGrain _roomGrain = roomGrain;

    public async Task<TradePartiesSnapshot?> GetPartiesAsync(
        ActionContext ctx,
        RoomObjectId targetObjectId
    )
    {
        if (!_roomGrain.AvatarModule.TryGetPlayer(ctx.PlayerId, out _))
            return null;

        if (
            !_roomGrain._state.AvatarsByObjectId.TryGetValue(targetObjectId, out var avatar)
            || avatar is not IRoomPlayer partner
            || partner.PlayerId == ctx.PlayerId
        )
            return null;

        return new TradePartiesSnapshot
        {
            InitiatorId = ctx.PlayerId,
            PartnerId = partner.PlayerId,
            PartnerName = partner.Name,
            InitiatorMayTrade = await AllowsTradeByAsync(ctx.PlayerId),
            PartnerMayTrade = await AllowsTradeByAsync(partner.PlayerId),
        };
    }

    public void SetTradingStatus(ImmutableArray<PlayerId> playerIds, bool trading)
    {
        foreach (var playerId in playerIds)
        {
            if (!_roomGrain.AvatarModule.TryGetPlayer(playerId, out var player))
                continue;

            if (trading)
                player.AddStatus(AvatarStatusType.Trading, string.Empty);
            else
                player.RemoveStatus(AvatarStatusType.Trading);
        }
    }

    /// <summary>Whether a leaving player had a trade open, judged by the status the trade put on them.</summary>
    public bool IsTrading(PlayerId playerId) =>
        _roomGrain.AvatarModule.TryGetPlayer(playerId, out var player)
        && player.HasStatus(AvatarStatusType.Trading);

    private async Task<bool> AllowsTradeByAsync(PlayerId playerId) =>
        _roomGrain._state.RoomSnapshot.TradeType switch
        {
            RoomTradeModeType.Disabled => false,
            RoomTradeModeType.RoomOwnerAndRights =>
                await _roomGrain.SecurityModule.GetControllerLevelAsync(playerId)
                    >= RoomControllerType.Rights,
            _ => true,
        };
}
