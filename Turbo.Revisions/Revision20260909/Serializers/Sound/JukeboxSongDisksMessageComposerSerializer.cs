using Turbo.Primitives.Messages.Outgoing.Sound;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Sound;

internal class JukeboxSongDisksMessageComposerSerializer(int header)
    : AbstractSerializer<JukeboxSongDisksMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, JukeboxSongDisksMessageComposer message)
    {
        packet.WriteInteger(message.MaxLength).WriteInteger(message.Disks.Length);

        foreach (var disk in message.Disks)
            packet.WriteInteger(disk.DiskId).WriteInteger(disk.SongId);
    }
}
