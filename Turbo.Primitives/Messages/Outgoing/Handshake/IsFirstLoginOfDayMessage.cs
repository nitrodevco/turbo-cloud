using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Handshake;

[GenerateSerializer, Immutable]
public sealed record IsFirstLoginOfDayMessage : IComposer
{
    [Id(0)]
    public required bool IsFirstLoginOfDay { get; init; }
}
