using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Furniture.Snapshots;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;

namespace Turbo.Primitives.Policies;

/// <summary>
/// A policy over a player building in a room: placing a floor or wall item from their inventory
/// or the builders club, a sticky note from its pad, or saving the room's floor plan. It allows
/// or refuses with a reason the player is shown; the room's own checks (rights, the spot, its
/// limits) come after it. A public class implementing it is found in a plugin's assembly as it
/// loads and taken away as it unloads; see <see cref="IGamePolicies"/> for the order several run
/// in. Moving or picking up what is already placed is not asked.
/// <para>
/// It runs off the room's turn and may ask the room anything. A policy that throws is logged and
/// passed over: it allows.
/// </para>
/// </summary>
public interface IPlacementPolicy
{
    /// <summary>Lower runs first. 0 by default.</summary>
    int Order => 0;

    ValueTask<PlacementDecision> DecideAsync(PlacementRequest request, CancellationToken ct);
}

/// <summary>What a player builds.</summary>
public enum PlacementKind
{
    FloorItem,
    WallItem,

    /// <summary>The floor plan editor's save: <see cref="PlacementRequest.ModelData"/> is the new plan.</summary>
    FloorPlan,
}

/// <summary>
/// A player building in a room. <paramref name="Definition"/> is the furni's (null for a floor
/// plan, or a builders club offer whose furni the hotel does not know); <paramref name="X"/> and
/// <paramref name="Y"/> its tile (a wall item's wall position), 0 for a floor plan.
/// </summary>
public sealed record PlacementRequest(
    PlayerId PlayerId,
    RoomId RoomId,
    PlacementKind Kind,
    FurnitureDefinitionSnapshot? Definition = null,
    int X = 0,
    int Y = 0,
    bool BuildersClub = false,
    string? ModelData = null
);

/// <summary>A policy's answer: allowed, or refused with a reason the player is shown.</summary>
public sealed record PlacementDecision(bool Allowed, string? Reason = null)
{
    public static readonly PlacementDecision Allow = new(true);

    public static PlacementDecision Refuse(string? reason = null) => new(false, reason);
}
