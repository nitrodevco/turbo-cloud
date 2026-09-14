using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.RoomSettings;
using Turbo.Primitives.Messages.Outgoing.Roomsettings;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.PacketHandlers.RoomSettings;

/// <summary>
/// Sent when the owner opens the room settings window. The client only shows the window once the
/// settings data for the requested room id arrives, so nothing is sent when the player may not
/// edit the room.
/// </summary>
public class GetRoomSettingsMessageHandler(IGrainFactory grainFactory, IConfiguration configuration)
    : IMessageHandler<GetRoomSettingsMessage>
{
    private const string MAX_PLAYERS_LIMIT_KEY = "Turbo:Rooms:MaxPlayersLimit";
    private const int DEFAULT_MAX_PLAYERS_LIMIT = 50;

    private readonly IGrainFactory _grainFactory = grainFactory;
    private readonly IConfiguration _configuration = configuration;

    public async ValueTask HandleAsync(
        GetRoomSettingsMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || message.RoomId <= 0)
            return;

        var roomGrain = _grainFactory.GetRoomGrain(message.RoomId);
        var controllerLevel = await roomGrain
            .GetControllerLevelAsync(ctx.PlayerId, ct)
            .ConfigureAwait(false);

        if (controllerLevel < RoomControllerType.Owner)
            return;

        var snapshot = await roomGrain.GetSnapshotAsync().ConfigureAwait(false);

        if (!int.TryParse(_configuration[MAX_PLAYERS_LIMIT_KEY], out var maxPlayersLimit))
            maxPlayersLimit = DEFAULT_MAX_PLAYERS_LIMIT;

        await ctx.SendComposerAsync(
                new RoomSettingsDataEventMessageComposer
                {
                    RoomId = snapshot.RoomId,
                    Name = snapshot.Name,
                    Description = snapshot.Description,
                    DoorMode = snapshot.DoorMode,
                    CategoryId = snapshot.CategoryId,
                    MaximumVisitors = snapshot.PlayersMax,
                    MaximumVisitorsLimit = maxPlayersLimit,
                    Tags = snapshot.Tags,
                    TradeMode = snapshot.TradeType,
                    AllowPets = snapshot.AllowPets,
                    AllowFoodConsume = snapshot.AllowPetsEat,
                    AllowWalkThrough = snapshot.AllowBlocking,
                    HideWalls = snapshot.HideWalls,
                    WallThickness = snapshot.WallThickness,
                    FloorThickness = snapshot.FloorThickness,
                    ChatProtection = snapshot.ChatProtection,
                    LeaveOnDoorTileEnabled = false,
                    IdleSleepEnabled = false,
                    IdleSleepTimeoutSeconds = 0,
                    IdleAutokickEnabled = false,
                    IdleAutokickTimeoutSeconds = 0,
                    MuteAllPets = false,
                    ModSettings = snapshot.ModSettings,
                    HiddenByBc = false,
                },
                ct
            )
            .ConfigureAwait(false);
    }
}
