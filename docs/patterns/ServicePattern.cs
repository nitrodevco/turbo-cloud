using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Grains;

namespace Docs.Patterns;

// Reference-only sample: mirrors core service orchestration style.
public sealed class ServicePattern(IGrainFactory grainFactory)
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async Task<PlayerSummary?> TryGetPlayerSummaryAsync(
        PlayerId playerId,
        CancellationToken ct
    )
    {
        if (playerId <= 0)
        {
            return null;
        }

        var grain = _grainFactory.GetPlayerGrain(playerId);
        return await grain.GetSummaryAsync(ct).ConfigureAwait(false);
    }
}
