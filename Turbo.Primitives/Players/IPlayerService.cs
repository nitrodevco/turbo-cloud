using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Players;

/// <summary>
/// What the profile packets ask of the server. The handlers only check who is asking and send
/// back what a method returns; reading the player's grains and deciding what a viewer may see is
/// here.
/// </summary>
public interface IPlayerService
{
    /// <summary>
    /// A player's extended profile as <paramref name="viewerId"/> sees it, or null when there is
    /// no such player. A hidden profile (<c>PlayerSettingsSnapshot.ProfileHidden</c>) shown to
    /// anyone but its owner withholds groups, friend count and last login.
    /// </summary>
    public Task<IComposer?> GetExtendedProfileAsync(
        PlayerId viewerId,
        PlayerId playerId,
        CancellationToken ct
    );

    /// <summary><see cref="GetExtendedProfileAsync"/> for a player found by name.</summary>
    public Task<IComposer?> GetExtendedProfileByNameAsync(
        PlayerId viewerId,
        string playerName,
        CancellationToken ct
    );

    /// <summary>
    /// A player's relationship statuses (the profile's and the info stand's), empty for a hidden
    /// profile seen by anyone but its owner.
    /// </summary>
    public Task<IComposer> GetRelationshipStatusInfoAsync(
        PlayerId viewerId,
        PlayerId playerId,
        CancellationToken ct
    );
}
