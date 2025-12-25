using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Wired;

namespace Turbo.Rooms.Wired.Conditions;

public abstract class WiredCondition(IRoomFloorItemContext ctx)
    : WiredDefinition(ctx),
        IWiredCondition
{
    public abstract bool Evaluate(IWiredContext ctx);
}
