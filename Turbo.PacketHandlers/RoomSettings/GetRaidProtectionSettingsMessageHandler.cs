using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.RoomSettings;
using Turbo.Primitives.Messages.Outgoing.Roomsettings;
using Turbo.Primitives.Navigator;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.RoomSettings;

public class GetRaidProtectionSettingsMessageHandler(
    IGrainFactory grainFactory,
    INavigatorService navigatorService
) : IMessageHandler<GetRaidProtectionSettingsMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;
    private readonly INavigatorService _navigatorService = navigatorService;

    public async ValueTask HandleAsync(
        GetRaidProtectionSettingsMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (message.RoomId <= 0)
            return;

        // Asked before the room grain, whose activation would fail on a missing room.
        if (!await _navigatorService.RoomExistsAsync(message.RoomId, ct).ConfigureAwait(false))
            return;

        var settings = await _grainFactory
            .GetRoomGrain(message.RoomId)
            .GetRaidProtectionSettingsAsync(ctx.AsActionContext(), ct)
            .ConfigureAwait(false);

        // Refused, the client asks only for a room it was told it may manage, so it is not told
        // again.
        if (settings is null)
            return;

        await ctx.SendComposerAsync(
                new RaidProtectionSettingsMessageComposer { Settings = settings },
                ct
            )
            .ConfigureAwait(false);
    }
}
