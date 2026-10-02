using System;
using System.Globalization;

namespace Turbo.Primitives.Commands;

/// <summary>
/// How long a sanction lasts, typed as a number and a unit (<c>30m</c>, <c>12h</c>, <c>7d</c>,
/// <c>2w</c>) or <c>perm</c> for no end. A bare number is refused: whether it meant minutes or
/// days is how a one-hour ban turns into a one-year one.
/// </summary>
public readonly record struct CommandDuration(TimeSpan? Span)
{
    public const int MAX_YEARS = 10;

    public static CommandDuration Permanent { get; } = new(null);

    public bool IsPermanent => Span is null;

    /// <summary>When the sanction ends if it starts at <paramref name="now"/>; null for none.</summary>
    public DateTime? EndsAt(DateTime now) => Span is { } span ? now + span : null;

    public override string ToString() =>
        Span is not { } span ? "permanent"
        : span.TotalDays >= 1 && span.TotalDays % 1 == 0 ? $"{span.TotalDays:0} d"
        : span.TotalHours >= 1 && span.TotalHours % 1 == 0 ? $"{span.TotalHours:0} h"
        : $"{Math.Max(1, Math.Round(span.TotalMinutes)):0} min";

    public static bool TryParse(string text, out CommandDuration duration)
    {
        duration = default;

        var trimmed = text.Trim();

        if (
            trimmed.Equals("perm", StringComparison.OrdinalIgnoreCase)
            || trimmed.Equals("permanent", StringComparison.OrdinalIgnoreCase)
        )
        {
            duration = Permanent;

            return true;
        }

        if (trimmed.Length < 2)
            return false;

        if (
            !int.TryParse(
                trimmed[..^1],
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var amount
            )
            || amount <= 0
        )
            return false;

        var maxDays = 365 * MAX_YEARS;
        var unit = char.ToLowerInvariant(trimmed[^1]);
        var maxAmount = unit switch
        {
            'm' => maxDays * 24 * 60,
            'h' => maxDays * 24,
            'd' => maxDays,
            'w' => maxDays / 7,
            _ => 0,
        };

        if (maxAmount == 0 || amount > maxAmount)
            return false;

        var span = unit switch
        {
            'm' => TimeSpan.FromMinutes(amount),
            'h' => TimeSpan.FromHours(amount),
            'd' => TimeSpan.FromDays(amount),
            'w' => TimeSpan.FromDays(7d * amount),
            _ => throw new InvalidOperationException(),
        };

        duration = new CommandDuration(span);

        return true;
    }
}
