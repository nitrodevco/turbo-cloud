using Turbo.Primitives.Messages.Incoming.Inventory.Achievements;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Parsers.Inventory.Achievements;

internal class GetAchievementsMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) => new GetAchievementsMessage();
}
