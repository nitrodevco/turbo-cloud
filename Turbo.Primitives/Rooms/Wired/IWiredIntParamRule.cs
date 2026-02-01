using System;

namespace Turbo.Primitives.Rooms.Wired;

public interface IWiredIntParamRule
{
    public int DefaultValue { get; }
    public Type? ValueType { get; }

    public bool IsValid(int value);
    public int Sanitize(int value);
    public object FromInt(int value);
    public int ToInt(object value);
}
