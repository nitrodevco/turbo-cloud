using Turbo.Primitives.Messages.Incoming.Help;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Parsers.Help;

internal class GetPendingCallsForHelpMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) => new GetPendingCallsForHelpMessage();
}
