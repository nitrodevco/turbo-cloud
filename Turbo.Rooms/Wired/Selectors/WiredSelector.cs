using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Wired;

namespace Turbo.Rooms.Wired.Selectors;

public abstract class WiredSelector(IRoomObjectContext ctx) : WiredDefinition(ctx), IWiredSelector
{
    public abstract void Select(IWiredContext ctx);
}
