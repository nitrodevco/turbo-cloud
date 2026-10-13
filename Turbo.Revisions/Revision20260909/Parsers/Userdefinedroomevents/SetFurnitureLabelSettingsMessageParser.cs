using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;
using Turbo.Primitives.Rooms.Enums.Wired;

namespace Turbo.Revisions.Revision20260909.Parsers.Userdefinedroomevents;

internal class SetFurnitureLabelSettingsMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new SetFurnitureLabelSettingsMessage
        {
            ObjectId = packet.PopInt(),
            BubbleColor = packet.PopInt(),
            Visibility = (FurnitureLabelVisibilityType)packet.PopInt(),
            AlwaysOpen = packet.PopBoolean(),
        };
}
