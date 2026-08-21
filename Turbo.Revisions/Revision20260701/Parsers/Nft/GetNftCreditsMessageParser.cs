using Turbo.Primitives.Messages.Incoming.Nft;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260701.Parsers.Nft;

public class GetNftCreditsMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) => new GetNftCreditsMessage();
}
