namespace Turbo.Primitives.Rooms.Wired;

public interface IWiredCondition : IWiredDefinition
{
    public bool Evaluate(IWiredContext ctx);
}
