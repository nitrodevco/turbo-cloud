using Turbo.Primitives.Messages.Incoming.Moderator;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Parsers.Moderator;

internal class ModToolPreferencesMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new ModToolPreferencesMessage
        {
            WindowX = packet.PopInt(),
            WindowY = packet.PopInt(),
            WindowWidth = packet.PopInt(),
            WindowHeight = packet.PopInt(),
        };
}
