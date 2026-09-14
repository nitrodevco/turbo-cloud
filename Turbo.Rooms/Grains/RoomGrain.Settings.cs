using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Snapshots.Settings;

namespace Turbo.Rooms.Grains;

public sealed partial class RoomGrain
{
    public async Task<RoomSettingsSnapshot?> GetRoomSettingsAsync(
        ActionContext ctx,
        CancellationToken ct
    )
    {
        await SecurityModule.EnsureRightsLoadedAsync(ct);

        var controllerLevel = await SecurityModule.GetControllerLevelAsync(ctx);

        if (controllerLevel < RoomControllerType.Owner)
            return null;

        return new RoomSettingsSnapshot
        {
            Room = _state.RoomSnapshot,
            MaximumVisitorsLimit = _roomConfig.MaxPlayersLimit,
        };
    }
}
