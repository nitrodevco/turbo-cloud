using Turbo.Primitives.Orleans.Snapshots.Session;

namespace Turbo.Primitives;

public sealed record ActorContext
{
    public required ActorOrigin Origin { get; init; }
    public SessionKey SessionKey { get; init; } = SessionKey.Empty;
    public long PlayerId { get; init; } = -1;
    public long RoomId { get; init; } = -1;

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
