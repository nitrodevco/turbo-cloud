using Turbo.Primitives.Rooms.Wired;

namespace Turbo.Rooms.Wired.Variables;

public abstract class WiredVariable : WiredDefinition, IWiredVariable
{
    public abstract void Apply(IWiredContext ctx);
}
