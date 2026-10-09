using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.RoomSettings;
using Turbo.Primitives.Messages.Outgoing.Roomsettings;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.RoomSettings;

public class GetRaidProtectionSettingsMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<GetRaidProtectionSettingsMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        GetRaidProtectionSettingsMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || message.RoomId <= 0)
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
