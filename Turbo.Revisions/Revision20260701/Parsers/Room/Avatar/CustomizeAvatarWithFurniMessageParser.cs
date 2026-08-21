using Turbo.Primitives.Messages.Incoming.Room.Avatar;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260701.Parsers.Room.Avatar;

internal class CustomizeAvatarWithFurniMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) => new CustomizeAvatarWithFurniMessage();
}
