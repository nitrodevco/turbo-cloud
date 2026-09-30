using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Networking.Capabilities;

namespace Turbo.Primitives.Messages.Outgoing.Turbo;

/// <summary>
/// A Turbo extension, not Habbo's: the answer to <c>TurboClientCapabilitiesMessage</c>, each
/// extension the server accepted at the version both sides will use. Empty when it accepted none.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record TurboServerCapabilitiesMessage : IComposer
{
    [Id(0)]
    public required ImmutableArray<ClientCapabilitySnapshot> Capabilities { get; init; }
}
