namespace Turbo.Events.Players;

public sealed record PlayerEventEnvelope(
    long PlayerId,
    object Payload)
{
    public static PlayerEventEnvelope Create<T>(long playerId, T payload) => new(playerId, payload!);
}