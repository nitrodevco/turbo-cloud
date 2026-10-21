using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Turbo.Crypto.Configuration;
using Turbo.Messages.Registry;
using Turbo.Primitives.Crypto;
using Turbo.Primitives.Messages.Incoming.Handshake;
using Turbo.Primitives.Messages.Outgoing.Handshake;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Tracking;

namespace Turbo.PacketHandlers.Handshake;

[AllowAnonymous]
public class CompleteDiffieHandshakeMessageHandler(
    IDiffieService diffieService,
    IOptions<CryptoConfig> config,
    IClientTelemetry telemetry
) : IMessageHandler<CompleteDiffieHandshakeMessage>
{
    private readonly IDiffieService _diffieService = diffieService;
    private readonly CryptoConfig _config = config.Value;
    private readonly IClientTelemetry _telemetry = telemetry;

    public async ValueTask HandleAsync(
        CompleteDiffieHandshakeMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        // A second exchange would replace the session's keys mid-stream; the client opens a new
        // connection to start again.
        if (ctx.IsEncrypted)
        {
            await ctx.CloseSessionAsync("Diffie handshake completed twice").ConfigureAwait(false);

            return;
        }

        var sharedKey = _diffieService.GetSharedKey(message.SharedKey);

        await ctx.SendComposerAsync(
                new CompleteDiffieHandshakeMessageComposer
                {
                    PublicKey = _diffieService.GetPublicKey(),
                    ServerClientEncryption = _config.EnableServerToClientEncryption,
                },
                ct
            )
            .ConfigureAwait(false);

        ctx.SetupEncryption(sharedKey, _config.EnableServerToClientEncryption);
        _telemetry.RecordLoginStep(ctx.SessionKey, ctx.PlayerId, ClientLoginSteps.HANDSHAKE_OK);
    }
}
