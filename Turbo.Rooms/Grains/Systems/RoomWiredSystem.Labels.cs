using System.Collections.Immutable;
using System.Linq;
using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Rooms.Object.Logic.Furniture.Floor;

namespace Turbo.Rooms.Grains.Systems;

/// <summary>
/// The room's WIRED labels for a player who walks in. Habbo sends them on every entry, right after
/// <c>WiredEnvironment</c>, with no entries when the room has none (official client capture,
/// hhus 2026-10-13, three entries: <c>WiredEnvironment</c>, <c>FurnitureLabelsAndRegions</c>
/// <c>00000000</c>, <c>YouAreController</c>).
/// </summary>
public sealed partial class RoomWiredSystem
{
    /// <summary>
    /// What a player who walks in is told about the room's wired, in Habbo's order. It goes in
    /// their entry (<c>RoomGrain.BuildEntryTailAsync</c>), after the room's configuration states.
    /// </summary>
    public ImmutableArray<IComposer> GetEntryState() =>
        [
            BuildEnvironment(),
            new FurnitureLabelsAndRegionsMessageComposer
            {
                Entries =
                [
                    .. FurniModule
                        .Items.OfType<IRoomFloorItem>()
                        .Select(x => x.Logic)
                        .OfType<FurnitureLabelLogic>()
                        .Select(x => x.GetLabelSnapshot()),
                ],
            },
            .. GetVisualizationEntryState(),
        ];
}
