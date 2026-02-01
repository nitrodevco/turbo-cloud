using System;

namespace Turbo.Rooms.Wired.IntParams;

public sealed class WiredIntBoolRule(bool defaultValue) : WiredIntParamRule(defaultValue ? 1 : 0)
{
    public override Type? ValueType { get; } = typeof(bool);

    public override bool IsValid(int value) => value == 0 || value == 1;

    public override object FromInt(int value) => value != 0;

    public override int ToInt(object value)
    {
        if (value is bool b)
            return b ? 1 : 0;

        throw new InvalidCastException("Expected bool.");
    }
}
