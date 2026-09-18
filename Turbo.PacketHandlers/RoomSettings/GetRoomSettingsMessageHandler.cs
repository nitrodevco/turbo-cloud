using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.RoomSettings;
using Turbo.Primitives.Messages.Outgoing.Roomsettings;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.RoomSettings;

/// <summary>
/// Sent when the owner opens the room settings window. The client only shows the window once the
/// settings data for the requested room id arrives; a refused or unknown room gets NoSuchFlat so
/// the client stops waiting.
/// </summary>
public class GetRoomSettingsMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<GetRoomSettingsMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        GetRoomSettingsMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || message.RoomId <= 0)
            return;

        var settings = await _grainFactory
            .GetRoomGrain(message.RoomId)
            .GetRoomSettingsAsync(ctx.AsActionContext(), ct)
            .ConfigureAwait(false);

        if (settings is null)
        {
            await ctx.SendComposerAsync(
                    new NoSuchFlatEventMessageComposer { RoomId = message.RoomId },
                    ct
                )
                .ConfigureAwait(false);

            return;
        }

        var room = settings.Room;

        await ctx.SendComposerAsync(
                new RoomSettingsDataEventMessageComposer
                {
                    RoomId = room.RoomId,
                    Name = room.Name,
                    Description = room.Description,
                    DoorMode = room.DoorMode,
                    CategoryId = room.CategoryId,
                    MaximumVisitors = room.PlayersMax,
                    MaximumVisitorsLimit = settings.MaximumVisitorsLimit,
                    Tags = room.Tags,
                    TradeMode = room.TradeType,
                    AllowPets = room.AllowPets,
                    AllowFoodConsume = room.AllowPetsEat,
                    AllowWalkThrough = room.AllowBlocking,
                    HideWalls = room.HideWalls,
                    WallThickness = room.WallThickness,
                    FloorThickness = room.FloorThickness,
                    ChatProtection = room.ChatProtection,
                    LeaveOnDoorTileEnabled = room.LeaveOnDoorTile,
                    IdleSleepEnabled = room.IdleSleepEnabled,
                    IdleSleepTimeoutSeconds = room.IdleSleepTimeoutSeconds,
                    IdleAutokickEnabled = room.IdleAutokickEnabled,
                    IdleAutokickTimeoutSeconds = room.IdleAutokickTimeoutSeconds,
                    MuteAllPets = room.MuteAllPets,
                    ModSettings = room.ModSettings,
                    HiddenByBc = room.HiddenByBc,
                },
                ct
            )
            .ConfigureAwait(false);
    }
}
