using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
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
    /// <summary>What a player who walks in is told about the room's wired, in Habbo's order.</summary>
    private async Task SendEntryStateAsync(PlayerId playerId, CancellationToken ct)
    {
        await SendClickUserEnvironmentAsync(playerId, ct);
        await SendFurnitureLabelsAsync(playerId, ct);
    }

    private Task SendFurnitureLabelsAsync(PlayerId playerId, CancellationToken ct) =>
        _roomGrain._grainFactory.SendComposerToPlayerAsync(
            playerId,
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
            ct
        );
}
