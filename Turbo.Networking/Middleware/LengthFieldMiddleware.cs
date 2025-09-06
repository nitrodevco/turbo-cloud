using System.Buffers;
using Turbo.Networking.Abstractions.Session;
using Turbo.Packets.Abstractions;

namespace Turbo.Networking.Middleware;

public class LengthFieldMiddleware : FrameMiddleware
{
    public override void Invoke(
        ref SequenceReader<byte> reader,
        ISessionContext ctx,
        ref IClientPacket? clientPacket
    )
    {
        ctx.ProcessSequenceForEncryption(ref reader);

        var r = reader;

        if (r.Length < 4 || !r.TryReadBigEndian(out int bodyLength) || r.Remaining < bodyLength)
            return;

        reader = r;
    }
}
