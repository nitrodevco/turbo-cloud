using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.RoomSettings;
using Turbo.Primitives.Messages.Outgoing.Roomsettings;
using Turbo.Primitives.Navigator;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Rooms.Snapshots.Settings;

namespace Turbo.PacketHandlers.RoomSettings;

/// <summary>
/// Saves the owner's room settings. The category is checked here, because only the navigator
/// knows which categories the player may use; the room grain checks everything else, including
/// that the player owns the room.
/// </summary>
public class SaveRoomSettingsMessageHandler(
    IGrainFactory grainFactory,
    INavigatorService navigatorService
) : IMessageHandler<SaveRoomSettingsMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;
    private readonly INavigatorService _navigatorService = navigatorService;

    public async ValueTask HandleAsync(
        SaveRoomSettingsMessage message,
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
            .SaveRoomSettingsAsync(
                ctx.AsActionContext(),
                new RoomSettingsUpdateSnapshot
                {
                    Name = message.RoomName,
                    Description = message.RoomDescription,
                    DoorMode = message.DoorMode,
                    Password = message.Password,
                    MaximumVisitors = message.MaxVisitors,
                    CategoryId = categoryId,
                    Tags = [.. message.Tags],
                    TradeMode = message.TradeMode,
                    AllowPets = message.AllowPets,
                    AllowPetsEat = message.AllowFoodConsume,
                    AllowWalkThrough = message.AllowWalkThrough,
                    HideWalls = message.HideWalls,
                    WallThickness = message.WallThickness,
                    FloorThickness = message.FloorThickness,
                    WhoCanMute = message.WhoCanMute,
                    WhoCanKick = message.WhoCanKick,
                    WhoCanBan = message.WhoCanBan,
                    ChatProtection = message.ChatFloodSensitivity,
                    LeaveOnDoorTile = message.LeaveOnDoorTileEnabled,
                    IdleSleepEnabled = message.IdleSleepEnabled,
                    IdleSleepTimeoutSeconds = message.IdleSleepTimeoutSeconds,
                    IdleAutokickEnabled = message.IdleAutokickEnabled,
                    IdleAutokickTimeoutSeconds = message.IdleAutokickTimeoutSeconds,
                    MuteAllPets = message.MuteAllPets,
                },
                ct
            )
            .ConfigureAwait(false);

        if (result.Succeeded)
        {
            await ctx.SendComposerAsync(
                    new RoomSettingsSavedEventMessageComposer { RoomId = message.RoomId },
                    ct
                )
                .ConfigureAwait(false);

            return;
        }

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
