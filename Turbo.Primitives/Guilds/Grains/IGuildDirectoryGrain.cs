using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Guilds.Snapshots;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;

namespace Turbo.Primitives.Guilds.Grains;

/// <summary>
/// What is true of groups across the whole hotel: their names and badges, and which room is
/// whose homeroom. One grain for the hotel, and every room activation and every badge drawn asks
/// it, so its methods answer from memory and the only queries are the reload.
///
/// It is a read-through cache. The <c>guilds</c> rows are the truth; a group grain that changes
/// its own row tells this grain afterwards, so nothing here is ever written back.
/// </summary>
public interface IGuildDirectoryGrain : IGrainWithStringKey
{
    /// <summary>
    /// The summaries for these groups, in the order asked. A group that does not exist is left
    /// out, so the result may be shorter than the request.
    /// </summary>
    public Task<ImmutableArray<GuildSummarySnapshot>> GetSummariesAsync(
        ImmutableArray<GuildId> guildIds,
        CancellationToken ct
    );

    /// <summary>The summary for one group, or null when there is no such group.</summary>
    public Task<GuildSummarySnapshot?> GetSummaryAsync(GuildId guildId, CancellationToken ct);

    /// <summary>
    /// The group whose homeroom this is, or null. This is what
    /// <c>RoomGrain.GetIsGroupRoomAsync</c> is asking, so it must not query.
    /// </summary>
    public Task<GuildSummarySnapshot?> GetGuildOfRoomAsync(RoomId roomId, CancellationToken ct);

    /// <summary>
    /// The homerooms of the hotel's groups, largest group first: the navigator's guild base
    /// search. How many come back is the module's config, not the caller's to choose.
    /// </summary>
    public Task<ImmutableArray<RoomId>> GetGuildBaseRoomIdsAsync(CancellationToken ct);

    /// <summary>
    /// Everything the badge editor may pick from. Hotel data that changes only when an operator
    /// edits it, so it is loaded with the rest of this grain and answered from memory.
    /// </summary>
    public Task<GuildEditorDataSnapshot> GetEditorDataAsync(CancellationToken ct);

    /// <summary>
    /// The badge a new group starts on before its creator touches the editor. Taken from the
    /// seeded parts rather than written down, so a hotel that reseeds gets a valid one.
    /// </summary>
    public Task<ImmutableArray<GuildBadgePartSnapshot>> GetDefaultBadgePartsAsync(
        CancellationToken ct
    );

    /// <summary>Groups whose name contains this text, for the navigator's group search.</summary>
    public Task<ImmutableArray<GuildSummarySnapshot>> SearchByNameAsync(
        string query,
        CancellationToken ct
    );

    /// <summary>
    /// A group was created or its summary changed. Passing the new summary rather than an id
    /// keeps this off the database on a path that already has the row in hand.
    /// </summary>
    public Task OnGuildChangedAsync(GuildSummarySnapshot summary, CancellationToken ct);

    /// <summary>A group was deleted; its homeroom is an ordinary room again.</summary>
    public Task OnGuildRemovedAsync(GuildId guildId, CancellationToken ct);

    /// <summary>
    /// How many groups this player owns. Creating a group checks it, and the directory is the
    /// only place that can answer without a query.
    /// </summary>
    public Task<int> GetOwnedCountAsync(PlayerId playerId, CancellationToken ct);
}
