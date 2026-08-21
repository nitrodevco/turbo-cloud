using Turbo.Primitives.Messages.Incoming.Poll;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260701.Parsers.Poll;

internal class PollAnswerMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) => new PollAnswerMessage();
}
