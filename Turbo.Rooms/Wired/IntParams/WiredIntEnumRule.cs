using System;
using System.Collections.Generic;
using System.Linq;

namespace Turbo.Rooms.Wired.IntParams;

public sealed class WiredIntEnumRule<TEnum> : WiredIntParamRule
    where TEnum : struct, Enum
{
    private readonly HashSet<int> _allowed;

    public WiredIntEnumRule(TEnum defaultValue)
        : base(Convert.ToInt32(defaultValue))
    {
        _allowed = [.. Enum.GetValues<TEnum>().Select(e => Convert.ToInt32(e))];

        if (!_allowed.Contains(DefaultValue))
            throw new ArgumentException("Default enum value is not in allowed set.");
    }

    public WiredIntEnumRule(TEnum defaultValue, params TEnum[] allowed)
        : base(Convert.ToInt32(defaultValue))
    {
        _allowed =
            (allowed is null)
                ? [.. Enum.GetValues<TEnum>().Select(e => Convert.ToInt32(e))]
                : [.. allowed.Select(e => Convert.ToInt32(e))];

        if (_allowed.Count == 0)
            throw new ArgumentException("Allowed set cannot be empty.", nameof(allowed));

        if (!_allowed.Contains(DefaultValue))
            throw new ArgumentException(
                "Default enum value is not in allowed set.",
                nameof(defaultValue)
            );
    }

    public override bool IsValid(int value) => _allowed.Contains(value);

    public override int Sanitize(int value) => _allowed.Contains(value) ? value : DefaultValue;
}
