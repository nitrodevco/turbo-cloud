using System;
using Turbo.Primitives.Rooms.Wired;

namespace Turbo.Rooms.Wired.IntParams;

public class WiredIntParamRule(int defaultValue) : IWiredIntParamRule
{
    public int DefaultValue { get; } = defaultValue;

    public virtual Type? ValueType => typeof(int);

    public virtual bool IsValid(int value) => true;

    public virtual int Sanitize(int value) => IsValid(value) ? value : DefaultValue;

    public virtual object FromInt(int value) => value;

    public virtual int ToInt(object value)
    {
        if (value is int i)
            return i;

        if (value is IConvertible c)
            return c.ToInt32(System.Globalization.CultureInfo.InvariantCulture);

        throw new InvalidCastException($"Cannot convert {value.GetType().Name} to int.");
    }
}
