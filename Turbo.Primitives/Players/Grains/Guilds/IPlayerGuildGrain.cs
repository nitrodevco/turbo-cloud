using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Guilds;
using Turbo.Primitives.Guilds.Snapshots;

namespace Turbo.Primitives.Players.Grains.Guilds;

/// <summary>
/// One player's side of the group system: which groups they belong to, which badge they wear,
/// and the making of a new group. It lives with the player rather than with the groups because
/// that is the direction everything asks in — the profile, the catalog's group picker, the
/// wired editor and room entry all start from a player and want their groups.
///
/// Creation is here for the same reason a purchase is on a per-player grain: it spends credits
/// and counts against a per-player limit, and one player clicking twice must not make two
/// groups. Orleans' single-threading is what stops that; there is no lock.
///
/// Memberships are held in memory, so the answer costs nothing; the guild directory supplies
/// the name and badge of each, which keeps this grain from holding a second copy of them.
/// </summary>
public interface IPlayerGuildGrain : IGrainWithIntegerKey
{
    /// <summary>
    /// The groups this player is a member of, in the shape the client reads them. Pending
    /// requests and blocks are not memberships and are left out.
    /// </summary>
    public Task<ImmutableArray<GuildInfoSnapshot>> GetMembershipsAsync(CancellationToken ct);

    /// <summary>
    /// The group whose badge this player wears, or null. A player who belongs to groups but has
    /// picked none has no favourite; nothing is chosen for them.
    /// </summary>
    public Task<GuildId?> GetFavouriteGuildIdAsync(CancellationToken ct);

    /// <summary>
    /// Picks the group whose badge this player wears, or clears it with null. A group they are
    /// not in is refused. The room they are standing in is told, because the badge on their
    /// avatar is what everybody else sees change.
    /// </summary>
    public Task SetFavouriteGuildAsync(GuildId? guildId, CancellationToken ct);

    /// <summary>What the create wizard opens on: the price, the eligible rooms and a starting badge.</summary>
    public Task<GuildCreationInfoSnapshot> GetCreationInfoAsync(CancellationToken ct);

    /// <summary>
    /// The rooms this player could still make a homeroom, which the edit window also lists. A
    /// room already taken by a group is not among them, except the group's own.
    /// </summary>
    public Task<ImmutableArray<GuildRoomOptionSnapshot>> GetRoomOptionsAsync(
        GuildId includeGuildRoomOf,
        CancellationToken ct
    );

    /// <summary>
    /// Makes a group, charges for it, and makes this player its owner. Everything the client
    /// sent is checked here; nothing is trusted from the wizard.
    /// </summary>
    public Task<GuildCreationResultSnapshot> CreateGuildAsync(
        GuildCreationRequestSnapshot request,
        CancellationToken ct
    );

    /// <summary>
    /// How many groups this player is in, for the membership cap. A group grain asks this
    /// before it lets somebody in; this grain must never call a group grain back, or the two
    /// would deadlock.
    /// </summary>
    public Task<int> GetMembershipCountAsync(CancellationToken ct);

    /// <summary>A membership was gained or lost elsewhere; the cached list is no longer right.</summary>
    public Task OnMembershipsChangedAsync(CancellationToken ct);
}
