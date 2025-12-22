using Turbo.Primitives.Rooms.Wired;

namespace Turbo.Rooms.Wired.Conditions;

public abstract class WiredCondition : WiredDefinition, IWiredCondition
{
    public abstract bool Evaluate(IWiredContext ctx);
}
