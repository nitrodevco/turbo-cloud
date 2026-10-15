using Turbo.Primitives.Messages.Incoming.Poll;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Parsers.Poll;

/// <summary>outgoing/poll/PollRejectComposer.as:9: the poll id.</summary>
internal class PollRejectMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new PollRejectMessage { PollId = packet.PopInt() };
}
