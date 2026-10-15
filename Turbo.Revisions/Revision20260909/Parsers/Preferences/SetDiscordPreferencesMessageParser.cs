using Turbo.Primitives.Messages.Incoming.Preferences;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Parsers.Preferences;

internal class SetDiscordPreferencesMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new SetDiscordPreferencesMessage
        {
            Version = packet.PopInt(),
            ShowHabbo = packet.PopBoolean(),
            ShareActivity = packet.PopBoolean(),
            HideInHiddenRooms = packet.PopBoolean(),
            AllowJoining = packet.PopBoolean(),
        };
}
