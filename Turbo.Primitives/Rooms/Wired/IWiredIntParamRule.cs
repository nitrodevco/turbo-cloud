namespace Turbo.Primitives.Rooms.Wired;

public interface IWiredIntParamRule
{
    public int DefaultValue { get; }
    public bool IsValid(int value);
    public int Sanitize(int value);
}
