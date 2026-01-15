using System.Collections.Generic;

namespace Turbo.Primitives.Rooms.Wired;

public interface IWiredStack
{
    public int StackId { get; }
    public List<IWiredTrigger> Triggers { get; }
    public List<IWiredSelector> Selectors { get; }
    public List<IWiredCondition> Conditions { get; }
    public List<IWiredAddon> Addons { get; }
    public List<IWiredAction> Actions { get; }
}
