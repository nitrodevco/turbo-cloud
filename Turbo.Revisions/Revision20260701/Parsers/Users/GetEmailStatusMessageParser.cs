using Turbo.Primitives.Messages.Incoming.Users;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260701.Parsers.Users;

internal class GetEmailStatusMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) => new GetEmailStatusMessage();
}
