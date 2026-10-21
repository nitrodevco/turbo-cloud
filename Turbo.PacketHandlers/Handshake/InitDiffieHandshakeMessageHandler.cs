using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Crypto;
using Turbo.Primitives.Messages.Incoming.Handshake;
using Turbo.Primitives.Messages.Outgoing.Handshake;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Tracking;

namespace Turbo.PacketHandlers.Handshake;

[AllowAnonymous]
public class InitDiffieHandshakeMessageHandler(
    IDiffieService diffieService,
    IClientTelemetry telemetry
) : IMessageHandler<InitDiffieHandshakeMessage>
{
    private readonly IDiffieService _diffieService = diffieService;
    private readonly IClientTelemetry _telemetry = telemetry;

    public async ValueTask HandleAsync(
        InitDiffieHandshakeMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        // A connection exchanges keys once; the client opens a new one to start again.
        if (ctx.IsEncrypted)
        {
            await ctx.CloseSessionAsync("Diffie handshake started on an encrypted session")
                .ConfigureAwait(false);

            return;
        }

        _telemetry.RecordLoginStep(ctx.SessionKey, ctx.PlayerId, ClientLoginSteps.HANDSHAKE_START);

        var prime = _diffieService.GetSignedPrime();
        var generator = _diffieService.GetSignedGenerator();

        await ctx.SendComposerAsync(
                new InitDiffieHandshakeMessageComposer { Prime = prime, Generator = generator },
                ct
            )
            .ConfigureAwait(false);
    }
}
