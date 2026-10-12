using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;

namespace Turbo.Primitives.Navigator;

/// <summary>A starter room made for a player: its id and the name it was given.</summary>
public sealed record CreatedStarterRoom(RoomId RoomId, string Name);

/// <summary>
/// The new user flow's starter rooms: a furnished room made for a new player from one of the
/// room types the client offers (<c>new.user.flow.roomTypes</c>, "10,11,12" in Habbo).
/// </summary>
public interface IStarterRoomService
{
    /// <summary>Whether a starter room of this type can be made.</summary>
    public bool HasType(string roomType);

    /// <summary>
    /// Makes the player a starter room of the type, furnished and painted as Habbo does; null
    /// when the type is unknown or the room could not be made (the player's room limit, its
    /// model missing from the hotel).
    /// </summary>
    public Task<CreatedStarterRoom?> CreateAsync(
        PlayerId playerId,
        string roomType,
        CancellationToken ct
    );
}
