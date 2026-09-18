using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Room.Action;
using Turbo.Primitives.Messages.Outgoing.Moderation;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.PacketHandlers.Room.Action;

/// <summary>
/// An ambassador warns a player; the target sees a moderator caution.
/// </summary>
public class AmbassadorAlertMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<AmbassadorAlertMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        AmbassadorAlertMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || ctx.RoomId <= 0 || message.UserId <= 0)
            return;

        // Ambassadors are a staff perk the server does not model yet; until it does, only
        // moderator-level controllers may send the caution, and everyone else is logged.
        var level = await _grainFactory
            .GetRoomGrain(ctx.RoomId)
            .GetControllerLevelAsync(ctx.PlayerId, ct)
            .ConfigureAwait(false);

        if (level < RoomControllerType.Moderator)
            return;

        await _grainFactory
            .GetPlayerPresenceGrain(message.UserId)
            .SendComposerAsync(
                new ModeratorCautionEventMessageComposer
                {
                    Message = AmbassadorAlertText.MESSAGE,
                    Url = string.Empty,
                },
                ct
            )
            .ConfigureAwait(false);
    }
}
