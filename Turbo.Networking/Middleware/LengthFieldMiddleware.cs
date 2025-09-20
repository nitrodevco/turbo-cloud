using System.Buffers;
using Turbo.Networking.Abstractions.Middleware;
using Turbo.Networking.Abstractions.Session;
using Turbo.Packets.Abstractions;

namespace Turbo.Networking.Middleware;

internal sealed class LengthFieldMiddleware : IFrameMiddleware
{
    public void Invoke(
        ref SequenceReader<byte> reader,
        ISessionContext ctx,
        ref IClientPacket? clientPacket
    )
    {
        var r = reader;

        if (r.Length < 4 || !r.TryReadBigEndian(out int bodyLength) || r.Remaining < bodyLength)
            return;

        reader = r;
    }
}
