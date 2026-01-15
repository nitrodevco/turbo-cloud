namespace Turbo.Primitives.Rooms.Wired;

public interface IWiredCondition : IWiredBox
{
    public int GetQuantifierCode();
    public bool GetIsInvert();
    public byte GetQuantifierType();
    public bool IsNegative();
    public bool Evaluate(IWiredProcessingContext ctx);
}
