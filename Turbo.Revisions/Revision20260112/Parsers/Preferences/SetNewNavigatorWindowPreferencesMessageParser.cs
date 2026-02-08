using Turbo.Primitives.Messages.Incoming.Preferences;
using Turbo.Primitives.Navigator.Enums;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Parsers.Preferences;

internal class SetNewNavigatorWindowPreferencesMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new SetNewNavigatorWindowPreferencesMessage
        {
            X = packet.PopInt(),
            Y = packet.PopInt(),
            Width = packet.PopInt(),
            Height = packet.PopInt(),
            OpenSavedSearches = packet.PopBoolean(),
            ResultsMode = (NavigatorViewModeType)packet.PopInt(),
        };
}
