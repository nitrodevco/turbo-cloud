using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Catalog.Enums;

namespace Turbo.Primitives.Catalog.Grains;

/// <summary>
/// The free products a player can claim once each (<c>HasClaimedProduct</c> / <c>ClaimProduct</c>),
/// keyed by the player id so two claims of the same product run one after the other.
/// </summary>
public interface IPlayerProductClaimGrain : IGrainWithIntegerKey
{
    /// <summary>Whether the player has taken the claim; false for a claim id nobody configured.</summary>
    Task<bool> HasClaimedAsync(string claimId, CancellationToken ct);

    /// <summary>
    /// Takes the claim with the player's choice (the client's option index, -1 when it sent none)
    /// and hands out what that choice gives.
    /// </summary>
    Task<ProductClaimResult> ClaimAsync(string claimId, int choice, CancellationToken ct);
}
