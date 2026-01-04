using System.Buffers;
using Turbo.Primitives.Packets;

namespace Turbo.Primitives.Networking;

public interface IClientPacketDecoder
{
    public IClientPacket TryRead(ref SequenceReader<byte> reader, ISessionContext ctx);
}
