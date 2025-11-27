using Turbo.Primitives.Orleans.Snapshots.Session;

namespace Turbo.Primitives.Actor;

public static class ActionContextFactory
{
    public static ActionContext ForPlayer(SessionKey sessionKey, long playerId, long roomId) =>
        new()
        {
            Origin = ActionOrigin.Player, // TODO make player again
            SessionKey = sessionKey,
            PlayerId = playerId,
            RoomId = roomId,
        };

    public static ActionContext ForSystem(long roomId) =>
        new() { Origin = ActionOrigin.System, RoomId = roomId };
}
