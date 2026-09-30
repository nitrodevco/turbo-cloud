using System.Collections.Immutable;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Networking.Capabilities;

namespace Turbo.Primitives.Messages.Incoming.Turbo;

/// <summary>A Turbo extension, not Habbo's: the protocol extensions a client understands.</summary>
public record TurboClientCapabilitiesMessage : IMessageEvent
{
    public required ImmutableArray<ClientCapabilitySnapshot> Capabilities { get; init; }
}
