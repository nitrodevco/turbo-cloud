using System;
using System.Collections.Immutable;
using Turbo.Primitives.Messages.Incoming.Turbo;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Networking.Capabilities;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Parsers.Turbo;

internal class TurboClientCapabilitiesMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet)
    {
        var count = Math.Clamp(packet.PopInt(), 0, ClientCapabilities.MAX_REQUESTED);
        var capabilities = ImmutableArray.CreateBuilder<ClientCapabilitySnapshot>();

        for (var i = 0; i < count; i++)
            capabilities.Add(
                new ClientCapabilitySnapshot
                {
                    Name = packet.PopString(),
                    Version = packet.PopInt(),
                }
            );

        return new TurboClientCapabilitiesMessage { Capabilities = capabilities.ToImmutable() };
    }
}
