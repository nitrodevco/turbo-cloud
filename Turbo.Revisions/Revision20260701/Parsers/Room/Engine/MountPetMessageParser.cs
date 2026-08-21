using Turbo.Primitives.Messages.Incoming.Room.Engine;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260701.Parsers.Room.Engine;

internal class MountPetMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) => new MountPetMessage();
}
