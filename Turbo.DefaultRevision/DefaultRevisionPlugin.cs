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
        _messageHub.Subscribe<CompleteDiffieHandshakeMessage>(
            this,
            OnCompleteDiffieHandshakeMessage
        );

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

    private async void OnInitDiffieHandshakeMessage(
        InitDiffieHandshakeMessage message,
        ISessionContext ctx
    )
    {
        var prime = _diffieService.GetSignedPrime();
        var generator = _diffieService.GetSignedGenerator();

        await ctx.SendComposerAsync(
            new InitDiffieHandshakeComposer { Prime = prime, Generator = generator }
        );
    }

    private async void OnCompleteDiffieHandshakeMessage(
        CompleteDiffieHandshakeMessage message,
        ISessionContext ctx
    )
    {
        var sharedKey = _diffieService.GetSharedKey(message.SharedKey);

        await ctx.SendComposerAsync(
            new CompleteDiffieHandshakeComposer { PublicKey = _diffieService.GetPublicKey() }
        );

        ctx.SetupEncryption(sharedKey);
    }
}
