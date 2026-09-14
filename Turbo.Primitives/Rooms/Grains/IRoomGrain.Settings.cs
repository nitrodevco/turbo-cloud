using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Rooms.Snapshots.Settings;

namespace Turbo.Primitives.Rooms.Grains;

public partial interface IRoomGrain
{
    /// <summary>
    /// The editable settings for the room settings window, or null when the caller may not
    /// edit the room.
    /// </summary>
    public Task<RoomSettingsSnapshot?> GetRoomSettingsAsync(
        ActionContext ctx,
        CancellationToken ct
    );
}
