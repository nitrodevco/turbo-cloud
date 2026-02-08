using Turbo.Primitives.Messages.Incoming.Nft;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Parsers.Nft;

internal class SaveUserNftWardrobeMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) => new SaveUserNftWardrobeMessage();
}
