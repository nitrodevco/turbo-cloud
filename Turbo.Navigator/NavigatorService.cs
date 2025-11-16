using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Turbo.Primitives.Navigator;
using Turbo.Primitives.Orleans.Snapshots.Navigator;
using Turbo.Primitives.Rooms;

namespace Turbo.Navigator;

public sealed class NavigatorService(
    ILogger<INavigatorService> logger,
    INavigatorProvider navigatorProvider,
    IRoomService roomService
) : INavigatorService
{
    private readonly ILogger<INavigatorService> _logger = logger;
    private readonly INavigatorProvider _navigatorProvider = navigatorProvider;
    private readonly IRoomService _roomService = roomService;

    public async Task<ImmutableArray<NavigatorTopLevelContextSnapshot>> GetTopLevelContextAsync() =>
        await _navigatorProvider.GetTopLevelContextsAsync().ConfigureAwait(false);

    public async Task<ImmutableArray<NavigatorSearchResultSnapshot>> GetSearchResultsAsync()
    {
        var rooms = await _navigatorProvider.GetRoomResultsAsync().ConfigureAwait(false);

        var activeRooms = await _roomService
            .GetRoomDirectory()
            .GetActiveRoomsAsync()
            .ConfigureAwait(false);

        var roomsList = activeRooms.ToDictionary(x => x.RoomId);

        return
        [
            .. rooms.Select(x =>
            {
                var activeRoom = roomsList.TryGetValue(x.RoomId, out var ar) ? ar : null;

                return new NavigatorSearchResultSnapshot
                {
                    RoomId = x.RoomId,
                    Name = activeRoom?.Name ?? x.Name,
                    OwnerId = activeRoom?.OwnerId ?? x.OwnerId,
                    OwnerName = activeRoom?.OwnerName ?? x.OwnerName,
                    DoorMode = x.DoorMode,
                    Population = activeRoom?.Population ?? 0,
                    PlayersMax = x.PlayersMax,
                    Description = activeRoom?.Description ?? x.Description,
                    TradeType = x.TradeType,
                    Score = x.Score,
                    Ranking = x.Ranking,
                    CategoryId = x.CategoryId,
                    Tags = x.Tags,
                    AllowPets = x.AllowPets,
                    AllowPetsEat = x.AllowPetsEat,
                    LastUpdatedUtc = x.LastUpdatedUtc,
                };
            }),
        ];
    }
}
