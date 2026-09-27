using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Handshake;

[GenerateSerializer, Immutable]
public sealed record DisconnectReasonEventMessageComposer : IComposer
{
    public const int ConcurrentLogin = 2;

    [Id(0)]
    public required int Reason { get; init; }
}
