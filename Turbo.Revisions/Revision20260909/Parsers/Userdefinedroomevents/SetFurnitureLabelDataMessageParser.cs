using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Parsers.Userdefinedroomevents;

internal class SetFurnitureLabelDataMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new SetFurnitureLabelDataMessage { ObjectId = packet.PopInt(), Text = packet.PopString() };
}
