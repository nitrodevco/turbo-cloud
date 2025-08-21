using System;
using System.Threading.Tasks;
using Turbo.Core.Authorization.Encryption;
using Turbo.Core.Networking.Session;
using Turbo.Core.Packets;
using Turbo.Core.Packets.Revisions;
using Turbo.Packets.Incoming.Handshake;

namespace Turbo.DefaultRevision;

public class DefaultRevisionPlugin(
    IRevisionManager revisionManager,
    IPacketMessageHub messageHub,
    IDiffieService diffieService
)
{
    private readonly IRevisionManager _revisionManager = revisionManager;
    private readonly IPacketMessageHub _messageHub = messageHub;
    private readonly IDiffieService _diffieService = diffieService;

    public Task InitializeAsync()
    {
        var revision = new RevisionDefault();
        _revisionManager.RegisterRevision(revision);

        _messageHub.Subscribe<ClientHelloMessage>(this, OnClientHelloMessage);
        _messageHub.Subscribe<InitDiffieHandshakeMessage>(this, OnInitDiffieHandshakeMessage);

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

        //ctx.SetRevision(revision.Revision);

        Console.WriteLine(message.Production);
    }

    private async void OnInitDiffieHandshakeMessage(
        InitDiffieHandshakeMessage message,
        ISessionContext ctx
    )
    {
        var prime = _diffieService.GetSignedPrime();
        var generator = _diffieService.GetSignedGenerator();

        Console.WriteLine(
            $"Received Diffie handshake request from {ctx.ChannelId} with prime {prime} and generator {generator}"
        );
    }
}
