using Turbo.Primitives.Messages.Incoming.Inventory.Avatareffect;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Parsers.Inventory.Avatareffect;

internal class AvatarEffectActivatedMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) => new AvatarEffectActivatedMessage();
}
