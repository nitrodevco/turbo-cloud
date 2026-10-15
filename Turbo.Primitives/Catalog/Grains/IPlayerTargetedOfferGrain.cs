using System.Threading;
using System.Threading.Tasks;
using Orleans;

namespace Turbo.Primitives.Catalog.Grains;

/// <summary>
/// A player's targeted offers (Flash <c>catalog/targetedoffers/OfferController</c>): which one
/// the player is offered, where they left its window, and buying it. Each answer is sent to the
/// player by the grain.
/// </summary>
public interface IPlayerTargetedOfferGrain : IGrainWithIntegerKey
{
    /// <summary>
    /// Answers <c>GetNextTargetedOffer</c>: the first running offer the player can still buy as
    /// <c>TargetedOffer</c>, or <c>TargetedOfferNotFound</c> when there is none.
    /// </summary>
    Task SendNextOfferAsync(CancellationToken ct);

    /// <summary>Remembers where the player left the offer's window (dialog or toolbar).</summary>
    Task SetTrackingStateAsync(int offerId, int trackingState, CancellationToken ct);

    /// <summary>
    /// Buys <paramref name="quantity"/> of the offer at its price and grants its catalogue offer
    /// that many times. False, with nothing taken, when the offer is not running for the player,
    /// the quantity is over what is left, or the balance is short.
    /// </summary>
    Task<bool> PurchaseAsync(int offerId, int quantity, CancellationToken ct);
}
