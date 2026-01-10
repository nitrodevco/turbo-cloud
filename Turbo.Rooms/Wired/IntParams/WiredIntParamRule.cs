namespace Turbo.Rooms.Wired.IntParams;

public abstract class WiredIntParamRule(int defaultValue)
{
    public int DefaultValue { get; } = defaultValue;

    public abstract bool IsValid(int value);

    public virtual int Sanitize(int value) => IsValid(value) ? value : DefaultValue;
}
