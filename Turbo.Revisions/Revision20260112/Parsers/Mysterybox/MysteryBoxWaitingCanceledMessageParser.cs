using Turbo.Primitives.Messages.Incoming.Mysterybox;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Parsers.Mysterybox;

internal class MysteryBoxWaitingCanceledMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) => new MysteryBoxWaitingCanceledMessage();
}
