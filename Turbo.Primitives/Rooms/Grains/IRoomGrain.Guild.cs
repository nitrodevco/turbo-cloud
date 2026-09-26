using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Guilds.Snapshots;
using Turbo.Primitives.Players;

namespace Turbo.Primitives.Rooms.Grains;

public partial interface IRoomGrain
{
    /// <summary>
    /// The group whose homeroom this room is, or null. Resolved through the guild directory once
    /// and held, so the many callers that only want to know whether this is a group room at all
    /// cost nothing.
    /// </summary>
    public Task<GuildSummarySnapshot?> GetGuildAsync(CancellationToken ct);

    /// <summary>
    /// A group's homeroom changed hands, was deleted, or had its rights level moved. The room
    /// forgets what it knew and re-reads it, then refreshes everybody standing in it — rights
    /// here are the group's, so a change to the group is a change to who may build.
    /// </summary>
    public Task OnGuildChangedAsync(CancellationToken ct);

    /// <summary>
    /// One player's standing in this room's group moved — they joined it, left it, or were
    /// promoted. Only their own rights here change, so only they are refreshed; a player who is
    /// not in the room costs one dictionary lookup.
    /// </summary>
    public Task RefreshGuildMemberAsync(PlayerId playerId, CancellationToken ct);

    /// <summary>
    /// A group changed its badge or its colours: every piece of that group's furni standing here
    /// is repainted and the room is shown the new look. A room holding none of it does nothing,
    /// which is the usual case and costs one dictionary scan.
    /// </summary>
    public Task RefreshGuildFurniAsync(Guilds.GuildId guildId, CancellationToken ct);

    /// <summary>
    /// Answers the menu that opens when somebody clicks a piece of guild furni standing here.
    /// Nothing is sent when the object is not guild furni, or wears no group.
    /// </summary>
    public Task SendGuildFurniContextMenuAsync(
        PlayerId viewerId,
        Object.RoomObjectId objectId,
        CancellationToken ct
    );

    /// <summary>
    /// The group that called this room home is gone. Every item standing here goes back to
    /// whoever owns it, which is what the client's delete confirmation promises, and the room
    /// stops being a group room.
    ///
    /// It is the room that does this rather than the group, because the items are the room's and
    /// because the group grain must not call a room grain. The group calls in, once, and this is
    /// the whole of what it asks for.
    /// </summary>
    public Task OnGuildDeletedAsync(CancellationToken ct);

    /// <summary>
    /// A player in the room changed the group badge they wear. Everyone is told so the badge on
    /// their avatar redraws.
    ///
    /// The new badge arrives as arguments rather than being read back from the player's guild
    /// grain, because that grain is what calls this: reading it back would have the two waiting
    /// on each other.
    /// </summary>
    public Task SetPlayerFavouriteGuildAsync(
        PlayerId playerId,
        int guildId,
        int guildStatus,
        string guildName,
        CancellationToken ct
    );
}
