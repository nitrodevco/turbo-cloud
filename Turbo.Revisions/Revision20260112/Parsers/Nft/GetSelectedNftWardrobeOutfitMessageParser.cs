using Turbo.Primitives.Messages.Incoming.Nft;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Parsers.Nft;

internal class GetSelectedNftWardrobeOutfitMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) => new GetSelectedNftWardrobeOutfitMessage();
}
