using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Outgoing.Inventory.Badges;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Inventory.Badges;

internal static class BadgeRequestExtensions
{
    /// <summary>
    /// Answers whether the sender now holds the badge behind a request code. An unknown code is
    /// answered too, as not fulfilled, so the client is never left waiting on it.
    /// </summary>
    public static async Task SendBadgeRequestFulfilledAsync(
        this MessageContext ctx,
        IGrainFactory grainFactory,
        string requestCode,
        string? badgeCode,
        CancellationToken ct
    )
    {
        var fulfilled =
            badgeCode is not null
            && await grainFactory
                .GetInventoryGrain(ctx.PlayerId)
                .HasBadgeAsync(badgeCode, ct)
                .ConfigureAwait(false);

        await ctx.SendComposerAsync(
                new IsBadgeRequestFulfilledEventMessageComposer
                {
                    RequestCode = requestCode,
                    Fulfilled = fulfilled,
                },
                ct
            )
            .ConfigureAwait(false);
    }
}
