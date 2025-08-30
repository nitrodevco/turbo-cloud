using System;
using System.Threading.Tasks;
using SuperSocket.Connection;
using Turbo.Core.Networking.Encryption;
using Turbo.Core.Networking.Session;
using Turbo.Core.Packets;
using Turbo.Core.Packets.Revisions;
using Turbo.Packets.Incoming.Handshake;
using Turbo.Packets.Outgoing.Handshake;

namespace Turbo.DefaultRevision;

public class DefaultRevisionPlugin(IRevisionManager revisionManager, IPacketMessageHub messageHub)
{
    private readonly IRevisionManager _revisionManager = revisionManager;
    private readonly IPacketMessageHub _messageHub = messageHub;

    public Task InitializeAsync()
    {
        var revision = new RevisionDefault();
        _revisionManager.RegisterRevision(revision);

        _messageHub.Subscribe<ClientHelloMessage>(this, OnClientHelloMessage);

        return Task.CompletedTask;
    }

    private async void OnClientHelloMessage(ClientHelloMessage message, ISessionContext ctx)
    {
        if (message.Production is null)
        {
            await ctx.CloseAsync(CloseReason.Rejected);

            return;
        }

        ctx.SetRevisionId(message.Production);
        Console.WriteLine($"Set revision to {message.Production} for session {ctx.SessionID}");
    }
}
