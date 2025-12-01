using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Handshake;

public sealed record AuthenticationOKMessage : IComposer
{
    public required int AccountId { get; init; }
    public required short[] SuggestedLoginActions { get; init; }
    public required int IdentityId { get; init; }
}
