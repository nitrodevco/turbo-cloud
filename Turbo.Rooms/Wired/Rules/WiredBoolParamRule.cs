using System;

namespace Turbo.Rooms.Wired.Rules;

public sealed class WiredBoolParamRule(bool defaultValue) : WiredParamRule(defaultValue ? 1 : 0)
{
    public override Type? ValueType { get; } = typeof(bool);

    /// <summary>
    /// Whether a value other than 0 or 1 is stored as the nearest of them instead of refusing the
    /// save, as Habbo does for some boxes (Furni Has Furni On: 2 is stored as 1, official client
    /// capture 2026-10-24).
    /// </summary>
    public bool Clamps { get; init; }

    public override bool IsValid(int value) => Clamps || value == 0 || value == 1;

    public override int Sanitize(int value) =>
        Clamps ? Math.Clamp(value, 0, 1) : base.Sanitize(value);

    public override object FromInt(int value) => value != 0;

    public override int ToInt(object value)
    {
        if (value is bool b)
            return b ? 1 : 0;

        throw new InvalidCastException("Expected bool.");
    }
}
