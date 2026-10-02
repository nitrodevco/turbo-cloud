using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Orleans.Observers;
using Turbo.Primitives.Players;

namespace Turbo.Primitives.Networking;

public interface ISessionGateway
{
    public ISessionContext? GetSession(SessionKey key);

    /// <summary>Every open connection at this moment, logged in or not.</summary>
    public IReadOnlyCollection<ISessionContext> GetSessions();
    public ISessionContextObserver? GetSessionObserver(SessionKey key);
    public PlayerId GetPlayerId(SessionKey key);
    public Task AddSessionAsync(SessionKey key, ISessionContext ctx);
    public Task RemoveSessionAsync(SessionKey key, CancellationToken ct);
    public Task AddSessionToPlayerAsync(SessionKey key, PlayerId playerId);
    public Task RemoveSessionFromPlayerAsync(PlayerId playerId, CancellationToken ct);

    /// <summary>Every player who has a logged-in connection at this moment.</summary>
    public IReadOnlyCollection<PlayerId> GetOnlinePlayerIds();

    /// <summary>
    /// Closes a player's connection, after sending <paramref name="farewell"/> when there is one.
    /// False when the player has no connection. For a ban, a disconnect and a closing hotel: a
    /// grain that wants a player out of a room still goes through the room.
    /// </summary>
    public Task<bool> DisconnectPlayerAsync(
        PlayerId playerId,
        IComposer? farewell,
        CancellationToken ct
    );
}
