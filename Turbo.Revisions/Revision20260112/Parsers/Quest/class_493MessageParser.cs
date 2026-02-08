using Turbo.Primitives.Messages.Incoming.Quest;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Parsers.Quest;

internal class class_493MessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) => new class_493Message();
}
