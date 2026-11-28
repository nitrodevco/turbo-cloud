using System.Linq;
using Turbo.Primitives.Messages.Incoming.Handshake;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Networking.Features;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Parsers.Handshake;

internal class ClientHelloMessageParser : IParser
{
    // A string's length prefix: the least one feature name occupies.
    private const int MIN_NAME_BYTES = sizeof(short);

    public IMessageEvent Parse(IClientPacket packet) =>
        new ClientHelloMessage
        {
            Production = packet.PopString(),
            Platform = packet.PopString(),
            ClientPlatform = packet.PopInt(),
            DeviceCategory = packet.PopInt(),
            // Build 116 (FLASH34) ends its hello with a count and that many feature names; an
            // older client's hello ends at the device category.
            Features =
                packet.Remaining >= sizeof(int)
                    ?
                    [
                        .. packet
                            .PopList(
                                MIN_NAME_BYTES,
                                static x => x.PopString(),
                                ClientFeatures.MAX_ADVERTISED
                            )
                            .Where(static x => x.Length <= ClientFeatures.MAX_NAME_LENGTH),
                    ]
                    : [],
        };
}
