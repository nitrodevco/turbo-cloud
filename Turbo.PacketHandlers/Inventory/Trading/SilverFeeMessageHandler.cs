using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Inventory.Trading;

namespace Turbo.PacketHandlers.Inventory.Trading;

/// <summary>
/// The silver fee belongs to collectible (NFT) trades, which this hotel does not run: no fee is
/// ever announced, so the client never asks for one and an offer to pay it is only recorded.
/// </summary>
public class SilverFeeMessageHandler(ILogger<SilverFeeMessageHandler> logger)
    : IMessageHandler<SilverFeeMessage>
{
    private readonly ILogger<SilverFeeMessageHandler> _logger = logger;

    public ValueTask HandleAsync(SilverFeeMessage message, MessageContext ctx, CancellationToken ct)
    {
        _logger.LogDebug(
            "Player {PlayerId} offered to pay a trade silver fee ({Pay}); collectible trades are not supported",
            ctx.PlayerId,
            message.Pay
        );

        return ValueTask.CompletedTask;
    }
}
