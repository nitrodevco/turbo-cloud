using System.Buffers;
using Turbo.Networking.Abstractions.Middleware;
using Turbo.Networking.Abstractions.Session;
using Turbo.Packets.Abstractions;

namespace Turbo.Networking.Middleware;

internal sealed class DecryptionMiddleware : IFrameMiddleware
{
    public void Invoke(
        ref SequenceReader<byte> reader,
        ISessionContext ctx,
        ref IClientPacket? clientPacket
    )
    {
        if (ctx.CryptoIn is null)
            return;

        var r = reader;
        var body = ctx.CryptoIn.Process(r.UnreadSpan.ToArray());

        r.Advance(body.Length);

        reader = new SequenceReader<byte>(new ReadOnlySequence<byte>(body));
    }
}
