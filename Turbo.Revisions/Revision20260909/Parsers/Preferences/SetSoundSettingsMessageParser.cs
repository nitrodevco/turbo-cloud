using Turbo.Primitives.Messages.Incoming.Preferences;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Parsers.Preferences;

internal class SetSoundSettingsMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new SetSoundSettingsMessage
        {
            GenericVolume = packet.PopInt(),
            FurniVolume = packet.PopInt(),
            TraxVolume = packet.PopInt(),
        };
}
