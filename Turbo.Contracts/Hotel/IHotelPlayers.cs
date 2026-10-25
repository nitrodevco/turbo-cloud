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

/// <summary>A player's Habbo Club: whether it runs now, and how many days the running one has lasted.</summary>
public readonly record struct HotelClub(bool Active, int StreakDays);

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

    /// <summary>The players online on this server now.</summary>
    [HotelCall(HotelCallContext.Anywhere)]
    IReadOnlyCollection<PlayerId> OnlineIds();

    /// <summary>The room the player is in, or null when they are in none.</summary>
    [HotelCall(HotelCallContext.NotFromItsGrain)]
    Task<RoomId?> CurrentRoomAsync(PlayerId player, CancellationToken ct);

    [HotelCall(HotelCallContext.NotFromItsGrain)]
    Task<HotelClub> ClubAsync(PlayerId player, CancellationToken ct);

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
