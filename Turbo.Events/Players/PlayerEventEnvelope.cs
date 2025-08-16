namespace Turbo.Events.Players;

using Orleans;

[GenerateSerializer]
public sealed record PlayerEventEnvelope(
    [property: Id(0)] long PlayerId,
    [property: Id(1)] object Payload)
{
    public static PlayerEventEnvelope Create<T>(long playerId, T payload) => new(playerId, payload!);
}
