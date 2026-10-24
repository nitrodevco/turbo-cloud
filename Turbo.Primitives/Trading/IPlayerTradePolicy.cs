using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Players;

namespace Turbo.Primitives.Trading;

/// <summary>
/// Whether a player may trade, by the hotel's trade rules: the <c>trade</c> permission and the
/// trading pass. Wired chests ask it before they move anything to or from a player; a plugin may
/// replace it to change who counts as able to trade.
/// </summary>
public interface IPlayerTradePolicy
{
    /// <summary>
    /// Whether the player may trade; <paramref name="givingAway"/> says whether they would give
    /// something away, as some pass rules let a player without the pass only receive.
    /// </summary>
    public Task<bool> MayTradeAsync(PlayerId playerId, bool givingAway, CancellationToken ct);
}
