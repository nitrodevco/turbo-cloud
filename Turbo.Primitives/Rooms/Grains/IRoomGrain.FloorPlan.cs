using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Snapshots.Mapping;

namespace Turbo.Primitives.Rooms.Grains;

public partial interface IRoomGrain
{
    /// <summary>
    /// Saves a floor plan drawn in the editor over this room's, and puts the room back together
    /// on it. Returns the players who were in the room, so the caller can stream it to them
    /// again: everything they were sent about the old plan is now wrong. Null when the save was
    /// refused, in which case nothing changed.
    /// </summary>
    /// <param name="properties">
    /// The door and the wall and floor settings, when the editor sent them. Null for a save that
    /// carries nothing but the heightmap, which leaves all of it as it was.
    /// </param>
    public Task<ImmutableArray<PlayerId>?> SaveFloorPlanAsync(
        ActionContext ctx,
        string modelData,
        FloorPlanPropertiesSnapshot? properties,
        CancellationToken ct
    );
}
