using System.Threading.Tasks;
using Turbo.Core.Networking.Session;
using Turbo.Core.Packets;
using Turbo.Core.Packets.Revisions;
using Turbo.Packets.Incoming.Handshake;

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
        var revision = _revisionManager.GetRevision(message.Production);

        if (revision is null)
        {
            await ctx.DisposeAsync();

            return;
        }

        ctx.SetRevision(revision.Revision);
    }
}
