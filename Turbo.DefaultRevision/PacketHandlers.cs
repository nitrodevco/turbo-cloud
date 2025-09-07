using System;
using System.Threading;
using System.Threading.Tasks;
using SuperSocket.Connection;
using Turbo.Messaging.Abstractions.Registry;
using Turbo.Primitives.Messages.Incoming.Handshake;

namespace Turbo.DefaultRevision;

public class PacketHandlers : IMessageHandler<ClientHelloMessage>
{
    public async Task HandleAsync(
        ClientHelloMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (message.Production is null)
        {
            await ctx.Session.CloseAsync(CloseReason.Rejected);

            return;
        }

        ctx.Session.SetRevisionId(message.Production);

        Console.WriteLine(
            $"Set revision to {message.Production} for session {ctx.Session.SessionID}"
        );
    }
}
