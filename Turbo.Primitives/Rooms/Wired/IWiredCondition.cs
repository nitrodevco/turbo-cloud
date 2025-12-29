namespace Turbo.Primitives.Rooms.Wired;

public interface IWiredCondition : IWiredItem
{
    public bool Evaluate(IWiredContext ctx);
}
