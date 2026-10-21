using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Turbo.Crypto.Configuration;
using Turbo.Messages.Registry;
using Turbo.Primitives.Crypto;
using Turbo.Primitives.Messages.Incoming.Handshake;
using Turbo.Primitives.Messages.Outgoing.Handshake;
using Turbo.Primitives.Tracking;

namespace Turbo.PacketHandlers.Handshake;

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
