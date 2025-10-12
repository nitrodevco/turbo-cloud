using System.Buffers;
using System.Text;
using SuperSocket.Connection;
using Turbo.Networking.Abstractions.Middleware;
using Turbo.Networking.Abstractions.Session;
using Turbo.Packets.Abstractions;

namespace Turbo.Networking.Decoder;

internal sealed class FlashPolicyDecoder : IFrameMiddleware
{
    private static readonly byte[] REQUEST = Encoding.ASCII.GetBytes("<policy-file-request/>\0");
    private static readonly byte[] RESPONSE = Encoding.ASCII.GetBytes(
        "<?xml version=\"1.0\"?>\r\n"
            + "<!DOCTYPE cross-domain-policy SYSTEM \"/xml/dtds/cross-domain-policy.dtd\">\r\n"
            + "<cross-domain-policy>\r\n"
            + "<allow-access-from domain=\"*\" to-ports=\"*\" />\r\n"
            + "</cross-domain-policy>\0"
    );

    public void Invoke(
        ref SequenceReader<byte> reader,
        ISessionContext ctx,
        ref IClientPacket? clientPacket
    )
    {
        if (ctx.PolicyDone)
            return;

        var r = reader;

        if (r.Length < REQUEST.Length)
            return;

        var prefix = r.UnreadSequence.Slice(0, REQUEST.Length);

        r.Advance(REQUEST.Length);

        reader = r;

        if (prefix.Equals(REQUEST))
        {
            ctx.PolicyDone = true;

            _ = ctx.SendAsync(RESPONSE);
            _ = ctx.CloseAsync(CloseReason.LocalClosing);

            return;
        }

        ctx.PolicyDone = true;
    }
}
