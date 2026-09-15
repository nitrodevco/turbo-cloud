using Turbo.Primitives.Messages.Incoming.Quest;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Parsers.Quest;

internal class FriendRequestQuestCompleteMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) => new FriendRequestQuestCompleteMessage();
}
