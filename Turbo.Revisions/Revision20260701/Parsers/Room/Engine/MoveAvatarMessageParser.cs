using Turbo.Primitives.Messages.Incoming.Room.Engine;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260701.Parsers.Room.Engine;

internal class MoveAvatarMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new MoveAvatarMessage { TargetX = packet.PopInt(), TargetY = packet.PopInt() };
}
