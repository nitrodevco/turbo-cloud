using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Wired;

namespace Turbo.Rooms.Wired.Variables;

public abstract class WiredVariable(IRoomFloorItemContext ctx)
    : WiredDefinition(ctx),
        IWiredVariable
{
    public abstract void Apply(IWiredContext ctx);
}
