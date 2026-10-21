using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.RoomSettings;
using Turbo.Primitives.Messages.Outgoing.Roomsettings;
using Turbo.Primitives.Navigator;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.RoomSettings;

public class UpdateRoomCategoryAndTradeSettingsMessageHandler(
    IGrainFactory grainFactory,
    INavigatorService navigatorService
) : IMessageHandler<UpdateRoomCategoryAndTradeSettingsMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;
    private readonly INavigatorService _navigatorService = navigatorService;

    public async ValueTask HandleAsync(
        UpdateRoomCategoryAndTradeSettingsMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (message.RoomId <= 0)
            return;

        // Asked before the room grain, whose activation would fail on a missing room.
        if (!await _navigatorService.RoomExistsAsync(message.RoomId, ct).ConfigureAwait(false))
            return;

        var categoryId = await _navigatorService
            .ResolvePlayerFlatCategoryAsync(ctx.PlayerId, message.CategoryId, ct)
            .ConfigureAwait(false);

        var result = await _grainFactory
            .GetRoomGrain(message.RoomId)
            .UpdateCategoryAndTradeSettingsAsync(
                ctx.AsActionContext(),
                categoryId,
                message.TradeType,
                ct
            )
            .ConfigureAwait(false);

        if (
            !await ctx.SendRoomSettingsSaveFailureAsync(message.RoomId, result, ct)
                .ConfigureAwait(false)
        )
            return;

        // Habbo confirms this save as it does the settings dialog's, first, ahead of the room's
        // RoomInfoUpdated, RoomVisualizationSettings and RoomChatSettings (official client capture
        // 2026-10-13, 2/2). The navigator reloads its room list on it
        // (NewIncomingMessages.onRoomSettingsSaved).
        await ctx.SendComposerAsync(
                new RoomSettingsSavedEventMessageComposer { RoomId = message.RoomId },
                ct
            )
            .ConfigureAwait(false);
    }
}
