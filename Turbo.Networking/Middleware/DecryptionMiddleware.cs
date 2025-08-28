using System;
using System.Buffers;
using System.Threading.Tasks;
using Turbo.Core.Networking.Protocol;
using Turbo.Core.Networking.Session;
using Turbo.Core.Packets.Messages;

namespace Turbo.Networking.Middleware;

public class DecryptionMiddleware : IFrameMiddleware
{
    public void Invoke(
        ref SequenceReader<byte> reader,
        ISessionContext ctx,
        ref IClientPacket? clientPacket
    )
    {
        var rc4 = ctx.Rc4Service;

        if (rc4 is null)
            return;

        var r = reader;

        var unread = r.UnreadSequence.Slice(0);

        r.Advance(unread.Length);

        var plain = unread.ToArray();

        rc4.ProcessBytes(plain);

        reader = new SequenceReader<byte>(new ReadOnlySequence<byte>(plain));
    }
}
