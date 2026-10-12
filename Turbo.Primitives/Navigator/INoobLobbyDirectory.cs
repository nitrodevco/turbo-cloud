using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;

namespace Turbo.Primitives.Navigator;

/// <summary>A new user lobby, with the players in it now.</summary>
public sealed record NoobLobbySnapshot(RoomId RoomId, string Name, int Players, int PlayersMax);

/// <summary>What making a room a lobby, or a room again, did.</summary>
public enum NoobLobbyChange
{
    Added,
    AlreadyAdded,
    Removed,
    NotALobby,
}

/// <summary>
/// The hotel's new user lobbies: rooms with door mode 4
/// (<see cref="Rooms.Enums.RoomDoorModeType.NoobLobby"/>), where Flash's "Enter the room!" offer
/// and toolbar send new users (<c>ForwardToSomeRoom("predefined_noob_lobby")</c>).
/// </summary>
public interface INoobLobbyDirectory
{
    /// <summary>The lobby link Flash forwards with.</summary>
    public const string FORWARD_DATA = "predefined_noob_lobby";

    public Task<ImmutableArray<NoobLobbySnapshot>> ListAsync(CancellationToken ct);

    /// <summary>
    /// The lobby to send a new user to: the fullest one with room left, so new users meet;
    /// null when the hotel has none or all are full.
    /// </summary>
    public Task<RoomId?> PickAsync(CancellationToken ct);

    /// <summary>
    /// Makes the room a lobby, or an open room again, and tells the client whether lobbies are
    /// on (<c>nux.lobbies.enabled</c>).
    /// </summary>
    public Task<NoobLobbyChange> SetAsync(
        RoomId roomId,
        bool isLobby,
        PlayerId by,
        CancellationToken ct
    );
}
