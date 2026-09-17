using System.Linq;
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
        if (ctx.PlayerId <= 0 || message.RoomId <= 0)
            return;

        var categoryId = _navigatorService
            .GetFlatCategoriesForPlayer(ctx.PlayerId)
            .Any(x => x.Id == message.CategoryId)
            ? message.CategoryId
            : (int?)null;

        var result = await _grainFactory
            .GetRoomGrain(message.RoomId)
            .UpdateCategoryAndTradeSettingsAsync(
                ctx.AsActionContext(),
                categoryId,
                message.TradeType,
                ct
            )
            .ConfigureAwait(false);

        if (result.Succeeded)
            return;

        await ctx.SendComposerAsync(
                new RoomSettingsSaveErrorEventMessageComposer
                {
                    RoomId = message.RoomId,
                    Error = result.Error,
                    Info = result.Info,
                },
                ct
            )
            .ConfigureAwait(false);
    }
}
