using System;
using System.Buffers;
using System.Text;
using System.Threading.Tasks;
using SuperSocket.Connection;
using Turbo.Core.Networking.Protocol;
using Turbo.Core.Networking.Session;
using Turbo.Core.Packets.Messages;

namespace Turbo.Networking.Middleware;

public class FlashPolicyMiddleware : IFrameMiddleware
{
    private static readonly byte[] Request = Encoding.ASCII.GetBytes("<policy-file-request/>\0");
    private static readonly byte[] Response = Encoding.ASCII.GetBytes(
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
