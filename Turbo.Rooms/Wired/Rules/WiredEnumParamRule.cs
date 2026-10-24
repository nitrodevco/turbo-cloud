using System;
using System.Collections.Generic;
using System.Linq;

namespace Turbo.Rooms.Wired.Rules;

public sealed class WiredEnumParamRule<TEnum> : WiredParamRule
    where TEnum : struct, Enum
{
    public override Type? ValueType { get; }

    private readonly HashSet<int> _allowed;

    public WiredEnumParamRule(TEnum defaultValue, params TEnum[] allowed)
        : base(Convert.ToInt32(defaultValue))
    {
        ValueType = typeof(TEnum);

        _allowed =
            (allowed is null || allowed.Length == 0)
                ? [.. Enum.GetValues<TEnum>().Select(e => Convert.ToInt32(e))]
                : [.. allowed.Select(e => Convert.ToInt32(e))];

        if (!_allowed.Contains(DefaultValue))
            throw new ArgumentException("Default enum value is not in allowed set.");
    }

    /// <summary>
    /// Whether a value outside the allowed ones is pulled to the nearest end of them instead of
    /// refusing the save, as Habbo does for some boxes (Click Configuration's [9, 9] is stored as
    /// [2, 1]; official client capture 2026-10-24).
    /// </summary>
    public bool ClampsOutOfRange { get; init; }

    public override bool IsValid(int value) => ClampsOutOfRange || _allowed.Contains(value);

    public override int Sanitize(int value) =>
        _allowed.Contains(value) ? value
        : ClampsOutOfRange ? Math.Clamp(value, _allowed.Min(), _allowed.Max())
        : DefaultValue;

    public override object FromInt(int value) => Enum.ToObject(typeof(TEnum), value);

    public override int ToInt(object value)
    {
        if (value is TEnum e)
            return Convert.ToInt32(e);

        throw new InvalidCastException($"Expected {typeof(TEnum).Name}.");
    }
}
