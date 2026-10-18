using Turbo.Primitives.Messages.Outgoing.Room.Session;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Room.Session;

/// <summary>
/// Habbo writes a short 2 after the header on every close (official client capture, 37 of 37
/// closes: room leave, full room, missing room). The client reads nothing
/// (CloseConnectionMessageParser, AS3 1745), so the value's meaning is unknown; it is written
/// as Habbo writes it.
/// </summary>
internal class CloseConnectionMessageComposerSerializer(int header)
    : AbstractSerializer<CloseConnectionMessageComposer>(header)
{
    private const short HABBO_CLOSE_VALUE = 2;

    protected override void Serialize(IServerPacket packet, CloseConnectionMessageComposer message)
    {
        packet.WriteShort(HABBO_CLOSE_VALUE);
    }
}
