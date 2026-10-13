using System.Collections.Immutable;
using Turbo.Primitives.Messages.Incoming.Moderator;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Parsers.Moderator;

internal class CloseIssueDefaultActionMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet)
    {
        var issueId = packet.PopInt();
        var ids = ImmutableArray.CreateBuilder<int>();

        for (int i = 0, n = packet.PopInt(); i < n; i++)
            ids.Add(packet.PopInt());

        return new CloseIssueDefaultActionMessage
        {
            IssueId = issueId,
            OtherIssueIds = ids.ToImmutable(),
            TopicId = packet.PopInt(),
        };
    }
}
