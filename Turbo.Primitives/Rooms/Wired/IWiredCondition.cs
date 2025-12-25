namespace Turbo.Primitives.Rooms.Wired;

public interface IWiredCondition
{
    public bool Evaluate(IWiredContext ctx);
}
