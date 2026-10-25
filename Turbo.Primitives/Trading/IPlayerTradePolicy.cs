using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Players;
using Turbo.Primitives.Trading.Enums;

namespace Turbo.Primitives.Trading;

/// <summary>
/// Whether a player may trade, by the hotel's trade rules: the <c>trade</c> permission and the
/// trading pass. Trades ask it of both players before they open, wired chests before they move
/// anything to or from a player, and the marketplace before a player sells; replacing it changes
/// who counts as able to trade in all three.
/// </summary>
public interface IPlayerTradePolicy
{
    /// <summary>What the player may do in a trade; the trade's refusal follows from it.</summary>
    public Task<TradeStandingType> GetTradeStandingAsync(PlayerId playerId, CancellationToken ct);
}

public static class PlayerTradePolicyExtensions
{
    /// <summary>
    /// Whether the player may trade; <paramref name="givingAway"/> says whether they would give
    /// something away, which a player who may only receive cannot.
    /// </summary>
    public static async Task<bool> MayTradeAsync(
        this IPlayerTradePolicy policy,
        PlayerId playerId,
        bool givingAway,
        CancellationToken ct
    ) =>
        await policy.GetTradeStandingAsync(playerId, ct) switch
        {
            TradeStandingType.Full => true,
            TradeStandingType.ReceiveOnly => !givingAway,
            _ => false,
        };
}
