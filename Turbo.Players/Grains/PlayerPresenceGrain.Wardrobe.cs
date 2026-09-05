using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Messages.Outgoing.Avatar;
using Turbo.Primitives.Orleans;

namespace Turbo.Players.Grains;

internal sealed partial class PlayerPresenceGrain
{
    private const int WARDROBE_STATE_LOADED = 1;

    public async Task OnRequestWardrobeAsync(CancellationToken ct)
    {
        var outfits = await _grainFactory
            .GetPlayerWardrobeGrain(_state.PlayerId)
            .GetOutfitsAsync(ct);

        await SendComposerAsync(
            new WardrobeMessageComposer { State = WARDROBE_STATE_LOADED, Outfits = outfits }
        );
    }
}
