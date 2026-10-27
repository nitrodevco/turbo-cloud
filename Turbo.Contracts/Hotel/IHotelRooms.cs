using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Furniture;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;
using Turbo.Primitives.Plugins.Messages;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Watching;

namespace Turbo.Contracts.Hotel;

/// <summary>A room as the hotel saved it, and whether it is on the navigator's public list.</summary>
public sealed record HotelRoom(
    RoomId Id,
    string Name,
    string Description,
    PlayerId Owner,
    string OwnerName,
    bool IsPublic
);

/// <summary>
/// The hotel's rooms: what they are, who is in them, and speaking to them. A send to a room no
/// one is in does nothing and does not load the room for it.
/// </summary>
public interface IHotelRooms
{
    /// <summary>The room, or null when there is none of that id.</summary>
    [HotelCall(HotelCallContext.OffGrain)]
    Task<HotelRoom?> GetAsync(RoomId room, CancellationToken ct);

    /// <summary>The rooms of the navigator's public list.</summary>
    [HotelCall(HotelCallContext.OffGrain)]
    Task<IReadOnlyCollection<RoomId>> PublicIdsAsync(CancellationToken ct);

    /// <summary>The players in the room now.</summary>
    [HotelCall(HotelCallContext.NotFromItsGrain)]
    Task<IReadOnlyList<PlayerId>> PlayersAsync(RoomId room, CancellationToken ct);

    /// <summary>A plugin message to everyone in the room; false when no one is in it.</summary>
    [HotelCall(HotelCallContext.NotFromItsGrain)]
    Task<bool> SendAsync(RoomId room, IPluginMessage message, CancellationToken ct);

    /// <summary>A composer to everyone in the room; false when no one is in it.</summary>
    [HotelCall(HotelCallContext.NotFromItsGrain)]
    Task<bool> SendAsync(RoomId room, IComposer composer, CancellationToken ct);

    /// <summary>A whisper from the hotel to the player in the room; false when they are not in it.</summary>
    [HotelCall(HotelCallContext.NotFromItsGrain)]
    Task<bool> WhisperAsync(RoomId room, PlayerId player, string text, CancellationToken ct);

    /// <summary>
    /// Hands <paramref name="message"/> to the plugin furni logic of the room's floor item
    /// <paramref name="itemId"/> (<c>FurniLogic.OnMessageAsync</c>), in the room's turn, and
    /// answers its reply: never throws for an item that is not there
    /// (<see cref="FurniLogicReplyStatus.UnknownItem"/>), runs another logic
    /// (<see cref="FurniLogicReplyStatus.WrongLogic"/>) or fails (<see cref="FurniLogicReplyStatus.Failed"/>).
    /// </summary>
    [HotelCall(HotelCallContext.NotFromItsGrain)]
    Task<FurniLogicReply> InvokeFurniLogicAsync(
        RoomId room,
        int itemId,
        FurniLogicMessage message,
        CancellationToken ct
    );

    /// <summary>Loads the room (its furni and their logic) if it is not loaded.</summary>
    [HotelCall(HotelCallContext.NotFromItsGrain)]
    Task LoadAsync(RoomId room, CancellationToken ct);

    /// <summary>
    /// Loads the room if it is not loaded and keeps it loaded and awake for
    /// <paramref name="lease"/> (1 s to 10 min) with no one in it: its pets, bots, rollers and
    /// periodic wired run as with someone in it, for a plugin drawing it somewhere else. The same
    /// <paramref name="holder"/> again renews the lease. A hold ends when its lease runs out, on
    /// <see cref="ReleaseAsync"/> or when the plugin unloads; then the room unloads 35 s later,
    /// as an empty room does. A room takes at most 256 holds, and a plugin holds at most 100
    /// rooms on a server (<see cref="RoomWatchLimits"/>).
    /// </summary>
    [HotelCall(HotelCallContext.NotFromItsGrain)]
    Task<RoomHoldOutcome> HoldAsync(
        RoomId room,
        string holder,
        TimeSpan lease,
        CancellationToken ct
    );

    /// <summary>Ends the plugin's hold <paramref name="holder"/> on the room; false when there was none.</summary>
    [HotelCall(HotelCallContext.NotFromItsGrain)]
    Task<bool> ReleaseAsync(RoomId room, string holder, CancellationToken ct);

    /// <summary>
    /// Lets <paramref name="player"/> watch <paramref name="room"/> from wherever they are: they
    /// are sent the room as an entry draws it, then everything the room sends the players in it,
    /// each packet inside <paramref name="envelope"/> (its fields, then the room's packet in its
    /// last field, an <c>IComposer</c> left null: <c>PluginEnvelopeComposer</c>), in order with
    /// their own room's packets, for a client module to read as that field. Only when they could
    /// walk in: not banned, and through its
    /// door (a doorbell, a password, a lobby for new users keep a watcher out; a full room does
    /// not). A watch holds the room, and ends when <paramref name="lease"/> (1 s to 10 min) runs
    /// out, on <see cref="UnwatchAsync"/>, when the player's session ends, when they enter the
    /// room or when the plugin unloads; calling it again renews it (and takes a new envelope). A
    /// player watches at most 4 rooms. A furni's logic watches the room its linked pair leads to
    /// through <c>IFurniContext.WatchRoomAsync</c>, whose door lets it in.
    /// </summary>
    /// <exception cref="System.ArgumentException">The envelope has no carried packet field.</exception>
    [HotelCall(HotelCallContext.NotFromItsGrain)]
    Task<RoomWatchOutcome> WatchAsync(
        PlayerId player,
        RoomId room,
        IPluginMessage envelope,
        TimeSpan lease,
        CancellationToken ct
    );

    /// <summary>Ends the player's watch of the room; false when they watched none for the plugin.</summary>
    [HotelCall(HotelCallContext.NotFromItsGrain)]
    Task<bool> UnwatchAsync(PlayerId player, RoomId room, CancellationToken ct);
}
