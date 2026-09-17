using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Handshake;

[GenerateSerializer, Immutable]
public sealed record AuthenticationOKMessage : IComposer
{
    [Id(0)]
    public required int AccountId { get; init; }

    [Id(1)]
    public required short[] SuggestedLoginActions { get; init; }

    [Id(2)]
    public required int IdentityId { get; init; }
}
