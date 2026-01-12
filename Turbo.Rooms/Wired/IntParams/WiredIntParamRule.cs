using Turbo.Primitives.Rooms.Wired;

namespace Turbo.Rooms.Wired.IntParams;

public class WiredIntParamRule(int defaultValue) : IWiredIntParamRule
{
    public int DefaultValue { get; } = defaultValue;

    public virtual bool IsValid(int value) => true;

    public virtual int Sanitize(int value) => IsValid(value) ? value : DefaultValue;
}
