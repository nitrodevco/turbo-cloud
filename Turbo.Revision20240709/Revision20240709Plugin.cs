using System;
using System.Threading.Tasks;
using Turbo.Authorization.Players.Policies;
using Turbo.Core.Authorization;
using Turbo.Core.Game.Players;
using Turbo.Core.Networking.Encryption;
using Turbo.Core.Networking.Session;
using Turbo.Core.Packets;
using Turbo.Core.Packets.Revisions;
using Turbo.Packets.Incoming.Handshake;
using Turbo.Packets.Outgoing.Handshake;
using Turbo.Packets.Outgoing.Users;

namespace Turbo.Revision20240709;

public class Revision20240709Plugin(
    IRevisionManager revisionManager,
    IPacketMessageHub messageHub,
    IDiffieService diffieService,
    IAuthorizationManager authorizationManager,
    IPlayerManager playerManager
)
{
    private readonly IRevisionManager _revisionManager = revisionManager;
    private readonly IPacketMessageHub _messageHub = messageHub;
    private readonly IDiffieService _diffieService = diffieService;
    private readonly IAuthorizationManager _authorizationManager = authorizationManager;
    private readonly IPlayerManager _playerManager = playerManager;

    public Task InitializeAsync()
    {
        var revision = new Revision20240709();
        _revisionManager.RegisterRevision(revision);

        _messageHub.Subscribe<CompleteDiffieHandshakeMessage>(
            this,
            OnCompleteDiffieHandshakeMessage
        );
        _messageHub.Subscribe<DisconnectMessage>(this, OnDisconnectMessage);
        _messageHub.Subscribe<InfoRetrieveMessage>(this, OnInfoRetrieveMessage);
        _messageHub.Subscribe<InitDiffieHandshakeMessage>(this, OnInitDiffieHandshakeMessage);
        _messageHub.Subscribe<SSOTicketMessage>(this, OnSSOTicketMessage);
        _messageHub.Subscribe<UniqueIdMessage>(this, OnUniqueIdMessage);
        _messageHub.Subscribe<VersionCheckMessage>(this, OnVersionCheckMessage);

        return Task.CompletedTask;
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

    private void OnDisconnectMessage(DisconnectMessage message, ISessionContext ctx) { }

    private async Task OnInfoRetrieveMessage(InfoRetrieveMessage message, ISessionContext ctx)
    {
        var player = await _playerManager.GetPlayerGrain(ctx.PlayerId);
        var summary = await player.GetAsync();

        await ctx.SendComposerAsync(new UserObjectMessage { Player = summary });
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

    private async void OnSSOTicketMessage(SSOTicketMessage message, ISessionContext ctx)
    {
        var ticket = message.SSO;

        var result = await _authorizationManager.AuthorizeAsync(ctx, new LoginPolicy());

        if (!result.Succeeded)
        {
            Console.WriteLine(result.Failures.ToString());
        }

        ctx.SetPlayerId(1);

        await ctx.SendComposerAsync(
            new AuthenticationOKMessage
            {
                AccountId = (int)ctx.PlayerId,
                SuggestedLoginActions = Array.Empty<short>(),
                IdentityId = (int)ctx.PlayerId,
            }
        );

        await ctx.SendComposerAsync(new ScrSendUserInfoMessage());
    }

    private void OnUniqueIdMessage(UniqueIdMessage message, ISessionContext ctx)
    {
        Console.WriteLine(
            "Unique ID Message: {0}:{1}:{2}",
            message.MachineID,
            message.Fingerprint,
            message.FlashVersion
        );
    }

    private void OnVersionCheckMessage(VersionCheckMessage message, ISessionContext ctx)
    {
        Console.WriteLine(
            "Version Check Message: {0}:{1}:{2}",
            message.ClientID,
            message.ClientURL,
            message.ExternalVariablesURL
        );
    }
}
