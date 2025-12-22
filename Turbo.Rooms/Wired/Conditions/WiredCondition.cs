using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Wired;

namespace Turbo.Rooms.Wired.Conditions;

public abstract class WiredCondition(IRoomObjectContext ctx) : WiredDefinition(ctx), IWiredCondition
{
    public abstract bool Evaluate(IWiredContext ctx);
}
