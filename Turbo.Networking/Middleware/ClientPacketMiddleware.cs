using System.Buffers;
using Turbo.Networking.Abstractions.Session;
using Turbo.Packets.Abstractions;

namespace Turbo.Networking.Middleware;

public class ClientPacketMiddleware : FrameMiddleware
{
    public override void Invoke(
        ref SequenceReader<byte> reader,
        ISessionContext ctx,
        ref IClientPacket? clientPacket
    )
    {
        var r = reader;

        r.TryReadBigEndian(out short header);

        var body = r.UnreadSpan.ToArray();

        r.Advance(body.Length);

        reader = r;
        clientPacket = new ClientPacket(header, body);
    }
}
