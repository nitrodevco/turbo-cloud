using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Wired;

namespace Turbo.Rooms.Wired.Variables;

public abstract class WiredVariable(IRoomObjectContext ctx) : WiredDefinition(ctx), IWiredVariable
{
    public abstract void Apply(IWiredContext ctx);
}
