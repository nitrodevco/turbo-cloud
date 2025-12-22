using Turbo.Primitives.Rooms.Wired;

namespace Turbo.Rooms.Wired.Selectors;

public abstract class WiredSelector : WiredDefinition, IWiredSelector
{
    public abstract void Select(IWiredContext ctx);
}
