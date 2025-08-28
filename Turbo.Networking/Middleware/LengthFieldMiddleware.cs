using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Threading.Tasks;
using Turbo.Core.Networking.Protocol;
using Turbo.Core.Networking.Session;
using Turbo.Core.Packets.Messages;

namespace Turbo.Networking.Middleware;

public class LengthFieldMiddleware : IFrameMiddleware
{
    public void Invoke(
        ref SequenceReader<byte> reader,
        ISessionContext ctx,
        ref IClientPacket? clientPacket
    )
    {
        var r = reader;

        if (r.Length < 6)
            return;

        if (r.Length < 6 || !r.TryReadBigEndian(out int bodyLength) || r.Remaining < bodyLength)
            return;

        reader = r;
    }
}
