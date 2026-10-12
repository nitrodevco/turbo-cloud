using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;

namespace Turbo.Primitives.Navigator;

/// <summary>A room the hotel offers as a starter room, as the directory lists it.</summary>
public sealed record StarterRoomSnapshot(
    RoomId RoomId,
    string RoomName,
    string RoomDescription,
    int SortOrder,
    string? Name,
    string? Description,
    DateTime? PictureTakenAt
);

/// <summary>What adding or removing a starter room did.</summary>
public enum StarterRoomChange
{
    Added,
    AlreadyAdded,
    Removed,
    NotAStarterRoom,
    RoomNotFound,
    Full,
}

/// <summary>The files a starter room's picture is drawn from (its render data, the camera's view) and into.</summary>
public sealed record StarterRoomPicturePaths(
    string RenderDataPath,
    string ScenePath,
    string PicturePath
);

/// <summary>
/// The hotel's own starter rooms (<c>starter_rooms</c>): rooms built in the hotel and offered to
/// new players, each copied for whoever picks it. Every change keeps the client's side in step -
/// the room types it offers (<c>new.user.flow.roomTypes</c>), where their pictures are
/// (<c>new.user.flow.room.image.url</c>) and their names and descriptions
/// (<c>room.name.&lt;id&gt;</c>, <c>room.description.&lt;id&gt;</c>) - so nothing has to be edited
/// by hand. With none, the built-in starter rooms are offered.
/// </summary>
public interface IStarterRoomDirectory
{
    /// <summary>The most rooms it offers (<c>Turbo:Onboarding:MaxStarterRooms</c>).</summary>
    public int Capacity { get; }

    public Task<ImmutableArray<StarterRoomSnapshot>> ListAsync(CancellationToken ct);

    /// <summary>The entry for the room, or null when it is not a starter room.</summary>
    public Task<StarterRoomSnapshot?> GetAsync(RoomId roomId, CancellationToken ct);

    /// <summary>Offers the room, last in the picker; refused past <c>MaxStarterRooms</c>.</summary>
    public Task<StarterRoomChange> AddAsync(RoomId roomId, PlayerId by, CancellationToken ct);

    public Task<StarterRoomChange> RemoveAsync(RoomId roomId, PlayerId by, CancellationToken ct);

    /// <summary>Where the room's picture's render data and picture go.</summary>
    public StarterRoomPicturePaths GetPicturePaths(RoomId roomId);

    /// <summary>The room's picture as a file, when it has been taken; null otherwise.</summary>
    public string? GetPictureFile(RoomId roomId);

    /// <summary>Records that the room's picture was taken, and points the client at it.</summary>
    public Task<bool> PictureTakenAsync(RoomId roomId, PlayerId by, CancellationToken ct);
}
