using Orleans.Runtime;

namespace Turbo.Contracts.Players;

public static class PlayerStreams
{
    public const string Provider = "SMS";
    public const string Namespace = "player-events";

    public static StreamId PlayerStreamId(string playerId) => StreamId.Create(Namespace, playerId);
}