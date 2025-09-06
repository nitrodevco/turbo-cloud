using System.Buffers;
using System.Text;
using SuperSocket.Connection;
using Turbo.Networking.Abstractions.Session;
using Turbo.Packets.Abstractions;

namespace Turbo.Networking.Middleware;

public class FlashPolicyMiddleware : FrameMiddleware
{
    private static readonly byte[] Request = Encoding.ASCII.GetBytes("<policy-file-request/>\0");
    private static readonly byte[] Response = Encoding.ASCII.GetBytes(
        "<?xml version=\"1.0\"?>\r\n"
            + "<!DOCTYPE cross-domain-policy SYSTEM \"/xml/dtds/cross-domain-policy.dtd\">\r\n"
            + "<cross-domain-policy>\r\n"
            + "<allow-access-from domain=\"*\" to-ports=\"*\" />\r\n"
            + "</cross-domain-policy>\0"
    );

    public override void Invoke(
        ref SequenceReader<byte> reader,
        ISessionContext ctx,
        ref IClientPacket? clientPacket
    )
    {
        // Already past policy stage?
        if (ctx.PolicyDone)
            return;

        var r = reader;

        if (r.Length < Request.Length)
            return;

        var prefix = r.UnreadSequence.Slice(0, Request.Length);

        r.Advance(Request.Length);

        reader = r;

        if (prefix.Equals(Request))
        {
            ctx.SendAsync(Response);
            ctx.CloseAsync(CloseReason.LocalClosing);
            ctx.PolicyDone = true;

            return;
        }

        ctx.PolicyDone = true;
    }
}
