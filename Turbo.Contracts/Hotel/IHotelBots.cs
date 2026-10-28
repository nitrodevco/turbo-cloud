using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;

namespace Turbo.Contracts.Hotel;

/// <summary>A bot standing in a room: its id, name, motto, owner and tile now.</summary>
public sealed record HotelBot(
    int Id,
    string Name,
    string Motto,
    PlayerId Owner,
    string OwnerName,
    RoomId Room,
    int X,
    int Y
);

/// <summary>What a bot can do with its body: the gestures a player's avatar has, without sleep and respect.</summary>
public enum HotelBotGesture
{
    Wave,
    BlowKiss,
    Laugh,
    Cry,
    Jump,
}

/// <summary>
/// The bots placed in rooms, told what to do by the plugin as a wired bot action tells them: an NPC
/// that answers, a guide that walks players to a furni. The room carries each out as Habbo does
/// for a bot: ten lines in five seconds at most, every line through the room's word filter. Like a
/// send, a room no one is in is left alone rather than loaded: its bots are not there and every
/// order answers false.
/// </summary>
public interface IHotelBots
{
    /// <summary>The bots standing in the room now; none when no one is in it.</summary>
    [HotelCall(HotelCallContext.NotFromItsGrain)]
    Task<IReadOnlyList<HotelBot>> InRoomAsync(RoomId room, CancellationToken ct);

    /// <summary>
    /// The bot says <paramref name="text"/> to the room, or shouts it. False when it is not in the
    /// room, the text is blank, or it said ten lines in the last five seconds.
    /// </summary>
    [HotelCall(HotelCallContext.NotFromItsGrain)]
    Task<bool> SayAsync(RoomId room, int botId, string text, bool shout, CancellationToken ct);

    /// <summary>The bot whispers to a player in the room; only they see it.</summary>
    [HotelCall(HotelCallContext.NotFromItsGrain)]
    Task<bool> WhisperAsync(
        RoomId room,
        int botId,
        PlayerId player,
        string text,
        CancellationToken ct
    );

    /// <summary>The bot walks to the tile; false when no way leads there.</summary>
    [HotelCall(HotelCallContext.NotFromItsGrain)]
    Task<bool> WalkToAsync(RoomId room, int botId, int x, int y, CancellationToken ct);

    /// <summary>The bot walks onto the room's floor furni <paramref name="itemId"/> (<see cref="IHotelRooms.FurniAsync"/>).</summary>
    [HotelCall(HotelCallContext.NotFromItsGrain)]
    Task<bool> WalkToFurniAsync(RoomId room, int botId, int itemId, CancellationToken ct);

    /// <summary>The bot waves, blows a kiss, laughs, cries or jumps.</summary>
    [HotelCall(HotelCallContext.NotFromItsGrain)]
    Task<bool> GestureAsync(RoomId room, int botId, HotelBotGesture gesture, CancellationToken ct);

    /// <summary>The bot dances dance 1 to 4, or stops with 0.</summary>
    [HotelCall(HotelCallContext.NotFromItsGrain)]
    Task<bool> DanceAsync(RoomId room, int botId, int dance, CancellationToken ct);

    /// <summary>The bot holds up sign 0 to 17, as a player's <c>:sign</c>.</summary>
    [HotelCall(HotelCallContext.NotFromItsGrain)]
    Task<bool> SignAsync(RoomId room, int botId, int sign, CancellationToken ct);
}
