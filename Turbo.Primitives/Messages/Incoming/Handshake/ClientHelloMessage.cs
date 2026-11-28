using System.Collections.Immutable;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Handshake;

public record ClientHelloMessage : IMessageEvent
{
    public required string Production { get; init; }

    public required string Platform { get; init; }

    public required int ClientPlatform { get; init; }

    public required int DeviceCategory { get; init; }

    /// <summary>
    /// The protocol features the client named after the fields above
    /// (<see cref="Networking.Features.ClientFeatures"/>), as it sent them; empty for a client
    /// that sends no list.
    /// </summary>
    public required ImmutableArray<string> Features { get; init; }
}
