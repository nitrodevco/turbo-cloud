using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Wired;

namespace Turbo.Rooms.Wired.Selectors;

public abstract class WiredSelector(IRoomFloorItemContext ctx)
    : WiredDefinition(ctx),
        IWiredSelector
{
    public abstract void Select(IWiredContext ctx);
}
