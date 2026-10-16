using Turbo.Primitives.Messages.Incoming.Collectibles;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Parsers.Collectibles;

internal class GetNftClaimsMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new GetNftClaimsMessage { WalletAddress = packet.PopString() };
}
