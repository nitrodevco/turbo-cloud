using Turbo.Primitives.Messages.Incoming.RoomSettings;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Parsers.RoomSettings;

internal class GetRaidProtectionSettingsMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new GetRaidProtectionSettingsMessage { RoomId = packet.PopInt() };
}
