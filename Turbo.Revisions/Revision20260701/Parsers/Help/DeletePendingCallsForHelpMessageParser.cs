using Turbo.Primitives.Messages.Incoming.Help;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260701.Parsers.Help;

internal class DeletePendingCallsForHelpMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) => new DeletePendingCallsForHelpMessage();
}
