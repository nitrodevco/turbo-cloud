using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Rooms.Snapshots.Mapping;

namespace Turbo.Primitives.Rooms;

public partial interface IRoomService
{
    /// <summary>
    /// Saves a floor plan drawn in the editor, then reloads the room the way Habbo does: everybody
    /// in it is sent out and, after <c>RoomConfig.FloorPlanSaveReturnDelayMs</c>, forwarded back in.
    /// </summary>
    public Task SaveFloorPlanAsync(
        ActionContext ctx,
        string modelData,
        FloorPlanPropertiesSnapshot? properties,
        CancellationToken ct
    );
}
