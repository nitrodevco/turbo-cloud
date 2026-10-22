using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Players.Onboarding.Snapshots;

namespace Turbo.Primitives.Players.Grains;

/// <summary>
/// A new player's gifts (<c>Turbo:Onboarding:Gifts</c>): which steps are offered to them now and
/// the picks they make, each step taken once. Keyed by the player id, so two answers run one after
/// the other.
/// </summary>
public interface IPlayerNuxGiftGrain : IGrainWithIntegerKey
{
    /// <summary>
    /// The steps due to the player that they have not picked from yet, empty when none is (or the
    /// player signed up too long ago).
    /// </summary>
    Task<ImmutableArray<NuxGiftStepSnapshot>> GetDueStepsAsync(CancellationToken ct);

    /// <summary>Sends the player the gift offer when a step is due, and nothing otherwise.</summary>
    Task OfferAsync(CancellationToken ct);

    /// <summary>
    /// Takes each pick of a due step not yet taken and gives what the gift is made of; a pick of a
    /// step not offered, taken already or with no such gift is left out. The number taken.
    /// </summary>
    Task<int> SelectAsync(
        ImmutableArray<NuxGiftSelectionSnapshot> selections,
        CancellationToken ct
    );
}
