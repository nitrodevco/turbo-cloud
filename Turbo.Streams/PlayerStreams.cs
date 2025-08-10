using System;
using System.Threading.Tasks;
using Orleans;
using Orleans.Runtime;
using Orleans.Streams;

namespace Turbo.Streams;

[StreamProvider(nameof(ProviderName))]
public class PlayerStreams
{
    public const string ProviderName = "PlayerEvents";

    public static StreamId Id(long playerId) => StreamId.Create($"player-{playerId}", Guid.Empty);
}