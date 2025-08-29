using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Threading.Tasks;
using Turbo.Core.Networking.Protocol;
using Turbo.Core.Networking.Session;
using Turbo.Core.Packets.Messages;
using Turbo.Packets.Incoming;

namespace Turbo.Networking.Middleware;

public class ClientPacketMiddleware : IFrameMiddleware
{
    public void Invoke(
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

        Console.WriteLine($"[Packet] 0x{header:X4} ({header})");
    }
}
