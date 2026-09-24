using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Guilds.Enums;
using Turbo.Primitives.Guilds.Snapshots;
using Turbo.Primitives.Players;

namespace Turbo.Primitives.Guilds.Grains;

/// <summary>
/// One group: its identity, its settings and its roster. Everything that changes a group goes
/// through here, so Orleans' single-threading is what keeps two admins acting on the same
/// membership at the same moment from both winning; no lock in this grain does that work.
///
/// The roster is held in memory as ids and ranks. It is bounded by the member limit in
/// <c>GuildConfig</c>, and holding it is what lets a view answer without a query. Names and
/// figures are not part of it — those belong to the players and are resolved when a packet
/// needs them.
///
/// This grain calls no room grain. From phase 5 the room grain asks it for a member's rank on
/// every controller-level check, and grains are not reentrant, so a call in the other direction
/// would deadlock the pair. It does call the guild directory, which never calls back.
///
/// A group that has been deleted, or never existed, activates empty and answers null. Callers
/// check rather than assuming a grain call found something.
/// </summary>
public interface IGuildGrain : IGrainWithIntegerKey
{
    /// <summary>The group itself, or null when there is no such group.</summary>
    public Task<GuildSnapshot?> GetSnapshotAsync(CancellationToken ct);

    /// <summary>
    /// The group as it looks to this viewer, or null when there is no such group. What the
    /// group grain cannot know — the homeroom's name, the owner's name, whether the viewer
    /// wears this badge — is not in it; the caller reads those beside this.
    /// </summary>
    public Task<GuildViewSnapshot?> GetViewAsync(PlayerId viewerId, CancellationToken ct);

    /// <summary>
    /// This player's rank, or null when they have no standing here at all. A pending request and
    /// a block are ranks, not nulls.
    /// </summary>
    public Task<GuildMemberRank?> GetMemberRankAsync(PlayerId playerId, CancellationToken ct);

    /// <summary>Members, not counting pending requests or blocked players.</summary>
    public Task<int> GetMemberCountAsync(CancellationToken ct);

    /// <summary>
    /// The group's current badge as the editor reads it, taken back out of the badge code so
    /// there is only ever one copy of it.
    /// </summary>
    public Task<ImmutableArray<GuildBadgePartSnapshot>> GetBadgePartsAsync(CancellationToken ct);

    /// <summary>
    /// Renames the group. False when the actor may not, or the group is gone. The name is
    /// clamped rather than refused, the way the editor clamps its own field.
    /// </summary>
    public Task<bool> UpdateIdentityAsync(
        PlayerId actorId,
        string name,
        string description,
        CancellationToken ct
    );

    /// <summary>Redraws the badge. Parts the editor was not offered are dropped.</summary>
    public Task<bool> UpdateBadgeAsync(
        PlayerId actorId,
        ImmutableArray<GuildBadgePartSnapshot> badgeParts,
        CancellationToken ct
    );

    /// <summary>Recolours the group, which recolours its furni everywhere it stands.</summary>
    public Task<bool> UpdateColorsAsync(
        PlayerId actorId,
        int primaryColorId,
        int secondaryColorId,
        CancellationToken ct
    );

    /// <summary>
    /// Changes who may join and who may decorate. Only the three types the editor offers are
    /// accepted; the hotel's own two are not a player's to set.
    /// </summary>
    public Task<bool> UpdateSettingsAsync(
        PlayerId actorId,
        GuildType guildType,
        GuildRightsLevel rightsLevel,
        CancellationToken ct
    );

    /// <summary>
    /// Joins, or asks to. Which of the two depends on the group's type, and the result says
    /// which happened so the caller does not have to work it out again.
    /// </summary>
    public Task<GuildJoinResultSnapshot> JoinAsync(PlayerId playerId, CancellationToken ct);

    /// <summary>Takes a pending request into the group.</summary>
    public Task<GuildMemberMgmtResultSnapshot> ApproveRequestAsync(
        PlayerId actorId,
        PlayerId targetId,
        CancellationToken ct
    );

    /// <summary>Turns a pending request down, which drops it entirely.</summary>
    public Task<GuildMemberMgmtResultSnapshot> RejectRequestAsync(
        PlayerId actorId,
        PlayerId targetId,
        CancellationToken ct
    );

    /// <summary>Takes in everybody currently waiting.</summary>
    public Task<GuildMemberMgmtResultSnapshot> ApproveAllRequestsAsync(
        PlayerId actorId,
        CancellationToken ct
    );

    /// <summary>Promotes a member to admin, or demotes one. The owner's alone to do.</summary>
    public Task<GuildMemberMgmtResultSnapshot> SetAdminAsync(
        PlayerId actorId,
        PlayerId targetId,
        bool isAdmin,
        CancellationToken ct
    );

    /// <summary>
    /// Removes a member, optionally blocking them from rejoining. Leaving is the same call with
    /// the actor as the target, which is how the client sends it.
    /// </summary>
    public Task<GuildMemberMgmtResultSnapshot> KickAsync(
        PlayerId actorId,
        PlayerId targetId,
        bool block,
        CancellationToken ct
    );

    /// <summary>Lets a blocked player back in as far as being able to ask again.</summary>
    public Task<GuildMemberMgmtResultSnapshot> UnblockAsync(
        PlayerId actorId,
        PlayerId targetId,
        CancellationToken ct
    );

    /// <summary>
    /// A page of the roster for the member window. How big a page is comes from the module's
    /// config, not from the caller.
    /// </summary>
    public Task<GuildMemberPageSnapshot?> GetMembersPageAsync(
        PlayerId viewerId,
        int pageIndex,
        string nameFilter,
        GuildMemberSearchType searchType,
        CancellationToken ct
    );

    /// <summary>
    /// Deletes the group and tells everyone who was in it. Owner only, and refused for a group
    /// larger than the hotel allows to be deleted at all.
    /// </summary>
    public Task<bool> DeactivateAsync(PlayerId actorId, CancellationToken ct);
}
