using System.Buffers;
using Turbo.Networking.Abstractions.Session;
using Turbo.Packets.Abstractions;

namespace Turbo.Networking.Middleware;

public abstract class FrameMiddleware
{
    public abstract void Invoke(
        ref SequenceReader<byte> reader,
        ISessionContext ctx,
        ref IClientPacket? clientPacket
    );
}
