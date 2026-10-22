using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Players.Configuration;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Events;
using Turbo.Primitives.Rooms.Events.Player;

namespace Turbo.Players.Onboarding;

/// <summary>
/// Offers a new player their gifts (<see cref="OnboardingConfig.Gifts"/>) as they enter a room, as
/// long as a step is due that they have not picked from: the client's window cannot be closed, so
/// one left by a reload or a room change comes back at the next room. Habbo's own moment for the
/// offer is not recorded; its new user guide spoke in the player's room ("For your gift today, you
/// have three options. Please choose from the menu."). The room listener processor finds and
/// registers it.
/// </summary>
public sealed class NuxGiftRoomListener(
    IOptions<OnboardingConfig> config,
    IGrainFactory grainFactory,
    ILogger<NuxGiftRoomListener> logger
) : IRoomEventListener
{
    private readonly bool _enabled = config.Value.Gifts.Count > 0;

    public Task OnRoomEventAsync(RoomEvent evt, CancellationToken ct)
    {
        if (!_enabled || evt is not PlayerEnterEvent { PlayerId: var playerId } || playerId <= 0)
            return Task.CompletedTask;

        // Handed to the player's grain without waiting: a listener runs in the room's turn.
        grainFactory
            .GetPlayerNuxGiftGrain(playerId)
            .OfferAsync(CancellationToken.None)
            .LogAndForget(logger, "offer new user gifts to player {PlayerId}", playerId);

        return Task.CompletedTask;
    }
}
