using Turbo.Primitives.Orleans.Snapshots.Session;

namespace Turbo.Primitives.Actor;

public static class ActorContextFactory
{
    public static ActorContext ForPlayer(SessionKey sessionKey, long playerId, long roomId) =>
        new()
        {
            Origin = ActorOrigin.Player,
            SessionKey = sessionKey,
            PlayerId = playerId,
            RoomId = roomId,
        };

    public static ActorContext ForSystem(long roomId) =>
        new() { Origin = ActorOrigin.System, RoomId = roomId };
}
