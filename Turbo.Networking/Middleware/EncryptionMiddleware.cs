using System.Buffers;
using Turbo.Networking.Abstractions.Session;
using Turbo.Packets.Abstractions;

namespace Turbo.Networking.Middleware;

public class EncryptionMiddleware : FrameMiddleware
{
    public override void Invoke(
        ref SequenceReader<byte> reader,
        ISessionContext ctx,
        ref IClientPacket? clientPacket
    )
    {
        if (ctx.Rc4Engine is null)
            return;

        var r = reader;
        var body = ctx.Rc4Engine.ProcessBytes(r.UnreadSpan.ToArray());

        r.Advance(body.Length);

        reader = new SequenceReader<byte>(new ReadOnlySequence<byte>(body));
    }
}
