using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Users;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Users;

public class UpdateGuildSettingsMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<UpdateGuildSettingsMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        UpdateGuildSettingsMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || message.GuildId <= 0)
            return;

        var updated = await _grainFactory
            .GetGuildGrain(message.GuildId)
            .UpdateSettingsAsync(ctx.PlayerId, message.GuildType, message.RightsLevel, ct)
            .ConfigureAwait(false);

        if (!updated)
            return;

        // Who may build in the homeroom is this group's decision, so the room has to re-read it.
        // It is told from here rather than from the group grain because the room answers that
        // question by asking the group grain, and the two would wait on each other.
        var guild = await _grainFactory
            .GetGuildDirectoryGrain()
            .GetSummaryAsync(message.GuildId, ct)
            .ConfigureAwait(false);

        if (guild is null)
            return;

        await _grainFactory
            .GetRoomGrain(guild.RoomId)
            .OnGuildChangedAsync(ct)
            .ConfigureAwait(false);
    }
}
