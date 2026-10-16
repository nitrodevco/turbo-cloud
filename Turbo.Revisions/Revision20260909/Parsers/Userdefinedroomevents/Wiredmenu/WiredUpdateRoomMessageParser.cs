using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents.Wiredmenu;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Parsers.Userdefinedroomevents.Wiredmenu;

/// <summary>Flash <c>WiredUpdateRoomComposer</c>: one boolean, true for a rollback.</summary>
internal class WiredUpdateRoomMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new WiredUpdateRoomMessage { Rollback = packet.PopBoolean() };
}
