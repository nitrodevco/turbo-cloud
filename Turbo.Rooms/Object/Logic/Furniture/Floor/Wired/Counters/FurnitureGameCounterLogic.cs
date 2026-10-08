using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Counters;

/// <summary>
/// The Wired Game Counter (wf_game_upcounter): a wired counter that counts up like the Small
/// Wired Counter, and goes with the room's game (<c>~clock.is_game_aware</c>, which the official
/// client shows on it and not on the Small Wired Counter): it holds when the game ends.
/// </summary>
public class FurnitureGameCounterLogic(
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureCounterClockLogic(stuffDataFactory, ctx)
{
    public override bool IsGameAware => true;
}
