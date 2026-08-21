using Turbo.Primitives.Messages.Incoming.Gifts;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260701.Parsers.Gifts;

internal class VerifyCodeMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) => new VerifyCodeMessage();
}
