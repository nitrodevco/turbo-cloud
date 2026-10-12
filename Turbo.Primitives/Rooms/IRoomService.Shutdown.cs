using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Primitives.Rooms;

public partial interface IRoomService
{
    /// <summary>
    /// As the server stops, before the silo does: writes what every active room changed and
    /// unloads it. Left to the silo, each room's last changes (up to a hand-over interval) are
    /// lost, because by the time it deactivates the rooms it can no longer reach their
    /// persistence grains.
    /// </summary>
    public Task SaveAndUnloadAllRoomsAsync(CancellationToken ct);
}
