using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;
using Turbo.Primitives.Plugins.Messages;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Contracts.Hotel;

/// <summary>A player as the hotel saved them (online or not).</summary>
public sealed record HotelPlayer(
    PlayerId Id,
    string Name,
    string Figure,
    AvatarGenderType Gender,
    string Motto,
    DateTime CreatedAt,
    DateTime? LastLoginAt
);

/// <summary>A player who was about lately, and whether they are online now.</summary>
public sealed record HotelRecentPlayer(HotelPlayer Player, bool Online);

/// <summary>
/// An account a player has elsewhere that signs them in: the service (<see cref="DISCORD"/>, the
/// one the hotel keeps links for), its id for the account there and its name there.
/// </summary>
public sealed record HotelIdentity(string Provider, string Id, string Name)
{
    public const string DISCORD = "discord";

    public static HotelIdentity Discord(string id, string name) => new(DISCORD, id, name);
}

/// <summary>What linking an identity to a player did.</summary>
public enum IdentityLinkOutcome
{
    /// <summary>Linked now; the player signs in with it from here on.</summary>
    Linked,

    /// <summary>It was already this player's: nothing changed.</summary>
    AlreadyLinked,

    /// <summary>Another player has it; only a replacing link (staff's) moves it.</summary>
    TakenByAnother,

    /// <summary>The player already has another account of that service; only a replacing link swaps it.</summary>
    PlayerHasAnother,

    /// <summary>There is no such player.</summary>
    NoSuchPlayer,

    /// <summary>The hotel keeps no links for that service.</summary>
    Unsupported,
}

/// <summary>A player's Habbo Club: whether it runs now, and how many days the running one has lasted.</summary>
public readonly record struct HotelClub(bool Active, int StreakDays);

/// <summary>
/// A player's time online: in all (every session's, the one running included) and in the
/// session running now (zero while they are offline).
/// </summary>
public readonly record struct HotelOnlineTime(TimeSpan Total, TimeSpan Session);

/// <summary>
/// The respects a player was given by other players (the figure their infostand shows) and the
/// ones they gave other players. Respects to and from pets are not counted.
/// </summary>
public readonly record struct HotelRespects(int Received, int Given);

/// <summary>A player's place on a toplist (1 is the top) and the figure it ranks.</summary>
public readonly record struct HotelRanked(int Rank, PlayerId Player, string Name, long Value);

/// <summary>
/// A figure players are ranked by: respects received, achievement score, or the balance of a
/// currency by its name in the hotel's currency types (<c>credits</c>).
/// </summary>
public sealed record PlayerStat
{
    private PlayerStat(string kind, string? currency)
    {
        Kind = kind;
        Currency = currency;
    }

    public string Kind { get; }

    /// <summary>The currency's name when <see cref="Kind"/> is <c>currency</c>.</summary>
    public string? Currency { get; }

    public static PlayerStat Respects { get; } = new("respects", null);

    public static PlayerStat AchievementScore { get; } = new("achievement_score", null);

    public static PlayerStat Balance(string currency) => new("currency", currency);
}

/// <summary>
/// The hotel's players: who they are, who is online and where, their club and permissions,
/// toplists, and sending to them. System accounts (the hotel's own bots and staff accounts marked
/// so) are never on a toplist.
/// </summary>
public interface IHotelPlayers
{
    /// <summary>The player, or null when there is none of that id.</summary>
    [HotelCall(HotelCallContext.OffGrain)]
    Task<HotelPlayer?> GetAsync(PlayerId player, CancellationToken ct);

    /// <summary>The player of exactly that name (any case), or null.</summary>
    [HotelCall(HotelCallContext.OffGrain)]
    Task<HotelPlayer?> FindAsync(string name, CancellationToken ct);

    /// <summary>The names of the players there are among <paramref name="players"/>, by id.</summary>
    [HotelCall(HotelCallContext.OffGrain)]
    Task<IReadOnlyDictionary<PlayerId, string>> NamesAsync(
        IReadOnlyCollection<PlayerId> players,
        CancellationToken ct
    );

    /// <summary>
    /// Players whose name starts with <paramref name="prefix"/> (any case), by name, at most
    /// <paramref name="take"/> (and never more than 50).
    /// </summary>
    [HotelCall(HotelCallContext.OffGrain)]
    Task<IReadOnlyList<HotelPlayer>> SearchAsync(string prefix, int take, CancellationToken ct);

    /// <summary>
    /// How many players were about since <paramref name="activeSince"/> (logged in then or later,
    /// or online now), or all there are with null. System accounts are not counted.
    /// </summary>
    [HotelCall(HotelCallContext.OffGrain)]
    Task<int> CountAsync(DateTime? activeSince, CancellationToken ct);

    /// <summary>
    /// The players about since <paramref name="activeSince"/>, the last to log in first (ties by
    /// id), <paramref name="take"/> of them (at most 100) after skipping <paramref name="skip"/>.
    /// System accounts are left out. Their figures are their looks, for a "who's been here" wall.
    /// </summary>
    [HotelCall(HotelCallContext.OffGrain)]
    Task<IReadOnlyList<HotelRecentPlayer>> RecentAsync(
        DateTime activeSince,
        int take,
        int skip,
        CancellationToken ct
    );

    /// <summary>The player the identity is linked to, or null.</summary>
    [HotelCall(HotelCallContext.OffGrain)]
    Task<PlayerId?> FindByIdentityAsync(string provider, string id, CancellationToken ct);

    /// <summary>
    /// Links the identity to the player, once the plugin has proven both are the same person (its
    /// own sign-in, a code shown in the hotel): an identity is one player's and a player has one
    /// of each service. <paramref name="replace"/> is staff's override: it moves the identity from
    /// whoever had it and drops the player's other one; gate the route asking for it with a
    /// permission node. A link made raises <c>PlayerIdentityLinkedEvent</c>.
    /// </summary>
    [HotelCall(HotelCallContext.OffGrain)]
    Task<IdentityLinkOutcome> LinkIdentityAsync(
        PlayerId player,
        HotelIdentity identity,
        bool replace,
        CancellationToken ct
    );

    /// <summary>Takes the player's link to that service away; false when they had none.</summary>
    [HotelCall(HotelCallContext.OffGrain)]
    Task<bool> UnlinkIdentityAsync(PlayerId player, string provider, CancellationToken ct);

    /// <summary>The players online on this server now.</summary>
    [HotelCall(HotelCallContext.Anywhere)]
    IReadOnlyCollection<PlayerId> OnlineIds();

    /// <summary>The room the player is in, or null when they are in none.</summary>
    [HotelCall(HotelCallContext.NotFromItsGrain)]
    Task<RoomId?> CurrentRoomAsync(PlayerId player, CancellationToken ct);

    [HotelCall(HotelCallContext.NotFromItsGrain)]
    Task<HotelClub> ClubAsync(PlayerId player, CancellationToken ct);

    /// <summary>
    /// The player's time online, or null when there is no such player. The total is saved as a
    /// session goes (every half minute by default), so it is never more than that behind.
    /// </summary>
    [HotelCall(HotelCallContext.OffGrain)]
    Task<HotelOnlineTime?> OnlineTimeAsync(PlayerId player, CancellationToken ct);

    /// <summary>The respects the player was given and gave, or null when there is no such player.</summary>
    [HotelCall(HotelCallContext.OffGrain)]
    Task<HotelRespects?> RespectsAsync(PlayerId player, CancellationToken ct);

    /// <summary>Whether the player holds the permission node (a plugin's own or core's), as every gate asks.</summary>
    [HotelCall(HotelCallContext.NotFromItsGrain)]
    Task<bool> HasPermissionAsync(PlayerId player, string node, CancellationToken ct);

    /// <summary>
    /// The players with the highest <paramref name="stat"/>, highest first (ties by id),
    /// <paramref name="take"/> of them (at most 100) after skipping <paramref name="skip"/>.
    /// </summary>
    [HotelCall(HotelCallContext.OffGrain)]
    Task<IReadOnlyList<HotelRanked>> TopByAsync(
        PlayerStat stat,
        int take,
        int skip,
        CancellationToken ct
    );

    /// <summary>A plugin message to the player's client; false when they are not online.</summary>
    [HotelCall(HotelCallContext.NotFromItsGrain)]
    Task<bool> SendAsync(PlayerId player, IPluginMessage message, CancellationToken ct);

    /// <summary>A composer to the player's client; false when they are not online.</summary>
    [HotelCall(HotelCallContext.NotFromItsGrain)]
    Task<bool> SendAsync(PlayerId player, IComposer composer, CancellationToken ct);
}
