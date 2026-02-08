using Turbo.Primitives.Messages.Incoming.Quest;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Parsers.Quest;

internal class GetConcurrentUsersRewardMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) => new GetConcurrentUsersRewardMessage();
}
