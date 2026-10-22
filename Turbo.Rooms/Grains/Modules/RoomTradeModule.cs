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
public sealed class RoomTradeModule(RoomGrain roomGrain) : RoomGrainComponent(roomGrain)
{
    public async Task<TradePartiesSnapshot?> GetPartiesAsync(
        ActionContext ctx,
        RoomObjectId targetObjectId
    )
    {
        if (!AvatarModule.TryGetPlayer(ctx.PlayerId, out _))
            return null;

        if (
            !AvatarModule.TryGetAvatar(targetObjectId, out var avatar)
            || avatar is not IRoomPlayer partner
            || partner.PlayerId == ctx.PlayerId
        )
            return null;

        var roomAllows = await AllowsTradeBetweenAsync(ctx.PlayerId, partner.PlayerId);

        return new TradePartiesSnapshot
        {
            InitiatorId = ctx.PlayerId,
            PartnerId = partner.PlayerId,
            PartnerName = partner.Name,
            InitiatorMayTrade = roomAllows,
            PartnerMayTrade = roomAllows,
        };
    }

    public void SetTradingStatus(ImmutableArray<PlayerId> playerIds, bool trading)
    {
        foreach (var playerId in playerIds)
        {
            if (!AvatarModule.TryGetPlayer(playerId, out var player))
                continue;

            if (trading)
                player.AddStatus(AvatarStatusType.Trading, string.Empty);
            else
                player.RemoveStatus(AvatarStatusType.Trading);
        }
    }

    /// <summary>Whether a leaving player had a trade open, judged by the status the trade put on them.</summary>
    public bool IsTrading(PlayerId playerId) =>
        AvatarModule.TryGetPlayer(playerId, out var player)
        && player.HasStatus(AvatarStatusType.Trading);

    /// <summary>
    /// Whether the room's trade mode lets these two trade. In a rights-only room one side
    /// holding rights is enough, so the owner can trade any visitor; group members without
    /// rights count as having none, as in the client's own check
    /// (<c>InfoStandWidgetHandler</c>, trade mode 1).
    /// </summary>
    private async Task<bool> AllowsTradeBetweenAsync(PlayerId initiatorId, PlayerId partnerId) =>
        State.RoomSnapshot.TradeType switch
        {
            RoomTradeModeType.Disabled => false,
            RoomTradeModeType.RoomOwnerAndRights => await HasTradeRightsAsync(initiatorId)
                || await HasTradeRightsAsync(partnerId),
            _ => true,
        };

    private async Task<bool> HasTradeRightsAsync(PlayerId playerId) =>
        await SecurityModule.GetControllerLevelAsync(playerId)
            is not (RoomControllerType.None or RoomControllerType.GroupRights);
}
