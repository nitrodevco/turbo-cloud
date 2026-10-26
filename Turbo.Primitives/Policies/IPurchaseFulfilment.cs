using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Primitives.Policies;

/// <summary>
/// Hands over a catalogue purchase in place of the hotel: an offer the plugin sells its own way
/// (a room ad that is the plugin's own promotion rather than the room event the hotel starts, a
/// product type of its own). The purchase stays the hotel's: the offer, the club level and the
/// purchase policies are asked as for any purchase, the price (as the policies left it) is taken
/// once, and <see cref="FulfilAsync"/> runs after it is paid; when it throws, the buyer gets the
/// money back and the client its purchase error. What the hotel would have handed over (the
/// items, the badge, the membership, the room event) and the checks only that needs (the room ad's
/// running event, a pet's name, a trophy's inscription) are left out. A public class implementing
/// it is found in a plugin's assembly as it loads and taken away as it unloads; of several, the
/// first (<see cref="Order"/>, then plugin key) that <see cref="Fulfils"/> the purchase takes it,
/// and with none the hotel hands over as always.
/// <para>
/// Asked for offers bought for oneself (the catalogue, the builders club catalogue) and room ads
/// (<see cref="PurchaseRequest.RoomAd"/>); a gift is always the hotel's. <see cref="Fulfils"/>
/// answers at once (from the request: the offer's product code, its products); both run on the
/// buyer's purchase turn and must not buy for the same player.
/// </para>
/// </summary>
public interface IPurchaseFulfilment
{
    /// <summary>Lower is asked first. 0 by default.</summary>
    int Order => 0;

    /// <summary>Whether this hands the purchase over instead of the hotel.</summary>
    bool Fulfils(PurchaseRequest request);

    /// <summary>
    /// Hands over what was bought, once it is paid for: <paramref name="paid"/> is the purchase
    /// as the policies left it (what was taken, how many units). Throwing refunds it.
    /// </summary>
    ValueTask FulfilAsync(PurchaseRequest request, PurchaseDecision paid, CancellationToken ct);
}
