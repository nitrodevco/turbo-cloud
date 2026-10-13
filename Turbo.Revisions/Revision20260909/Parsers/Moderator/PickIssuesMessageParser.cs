using System.Collections.Immutable;
using Turbo.Primitives.Messages.Incoming.Moderator;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Parsers.Moderator;

internal class PickIssuesMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet)
    {
        var ids = ImmutableArray.CreateBuilder<int>();

        for (int i = 0, n = packet.PopInt(); i < n; i++)
            ids.Add(packet.PopInt());

        return new PickIssuesMessage
        {
            IssueIds = ids.ToImmutable(),
            RetryEnabled = packet.PopBoolean(),
            RetryCount = packet.PopInt(),
            Message = packet.PopString(),
        };
    }
}
