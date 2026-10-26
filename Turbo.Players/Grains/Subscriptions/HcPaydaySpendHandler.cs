using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Events.Registry;
using Turbo.Primitives.Catalog.Events;
using Turbo.Primitives.Orleans;

namespace Turbo.Players.Grains.Subscriptions;

/// <summary>
/// Counts what a catalogue purchase cost in credits towards the buyer's coming HC Payday. Credit
/// furni do not count: Habbo's club centre read 152 credits for a non-member who had spent 155 in
/// the catalogue since the last payday, 3 of them on two credit furni coins (official client
/// captures 2026-10-17 to 2026-10-26; one sample, so the rule is inference).
/// </summary>
public sealed class HcPaydaySpendHandler(IGrainFactory grainFactory)
    : IEventHandler<PurchaseCompletedEvent>
{
    public async ValueTask HandleAsync(
        PurchaseCompletedEvent env,
        EventContext ctx,
        CancellationToken ct
    )
    {
        if (env.CostCredits <= 0 || env.GivesCreditFurni)
            return;

        await grainFactory
            .GetPlayerSubscriptionGrain(env.PlayerId)
            .RecordCatalogSpendAsync(env.CostCredits, ct);
    }
}
