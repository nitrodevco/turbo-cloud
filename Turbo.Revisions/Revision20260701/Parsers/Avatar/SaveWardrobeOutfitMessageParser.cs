using Turbo.Primitives.Messages.Incoming.Avatar;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260701.Parsers.Avatar;

internal class SaveWardrobeOutfitMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) => new SaveWardrobeOutfitMessage();
}
