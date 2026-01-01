namespace Turbo.Primitives.Rooms.Wired;

public interface IWiredCondition : IWiredItem
{
    public int GetQuantifierCode();
    public bool GetIsInvert();
    public byte GetQuantifierType();
    public bool Evaluate(IWiredContext ctx);
}
