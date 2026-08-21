using Turbo.Primitives.Messages.Incoming.Collectibles;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260701.Parsers.Collectibles;

internal class GetMintTokenOffersMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) => new GetMintTokenOffersMessage();
}
