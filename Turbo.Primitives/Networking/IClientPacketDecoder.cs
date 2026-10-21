using System.Buffers;
using Turbo.Primitives.Networking.Enums;
using Turbo.Primitives.Packets;

namespace Turbo.Primitives.Networking;

public interface IClientPacketDecoder
{
    /// <summary>
    /// Reads the next packet off <paramref name="reader"/>; <paramref name="packet"/> is set only
    /// when the result is <see cref="ClientPacketReadResult.Packet"/>. On
    /// <see cref="ClientPacketReadResult.Refused"/> the decoder has logged why and started closing
    /// <paramref name="ctx"/>, and the caller reads nothing more from that session.
    /// </summary>
    public ClientPacketReadResult TryRead(
        ref SequenceReader<byte> reader,
        ISessionContext ctx,
        out IClientPacket? packet
    );
}
