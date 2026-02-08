using Turbo.Primitives.Messages.Incoming.Talent;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Parsers.Talent;

internal class GuideAdvertisementReadMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) => new GuideAdvertisementReadMessage();
}
