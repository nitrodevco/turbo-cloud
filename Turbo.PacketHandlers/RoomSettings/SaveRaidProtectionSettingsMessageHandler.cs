using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.RoomSettings;
using Turbo.Primitives.Messages.Outgoing.Roomsettings;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.RoomSettings;

public class SaveRaidProtectionSettingsMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<SaveRaidProtectionSettingsMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        SaveRaidProtectionSettingsMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || message.RoomId <= 0)
            return;

        var result = await _grainFactory
            .GetRoomGrain(message.RoomId)
            .SaveRaidProtectionSettingsAsync(ctx.AsActionContext(), message.Settings, ct)
            .ConfigureAwait(false);

        await ctx.SendComposerAsync(
                new RaidProtectionSettingsResultMessageComposer { Result = result },
                ct
            )
            .ConfigureAwait(false);
    }
}
