using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Furniture;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;
using Turbo.Primitives.Plugins.Messages;
using Turbo.Primitives.Rooms;

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
}
