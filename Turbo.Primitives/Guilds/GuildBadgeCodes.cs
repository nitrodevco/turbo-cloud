using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using Turbo.Primitives.Guilds.Enums;
using Turbo.Primitives.Guilds.Snapshots;

namespace Turbo.Primitives.Guilds;

/// <summary>
/// The group badge code: how a set of badge parts becomes the one string the hotel passes around
/// and every renderer reads back.
///
/// The Flash client never builds or parses one — it hands the code to the imager named by
/// <c>group.badge.url</c> and draws the picture that comes back — so this format is a contract
/// between this server and whatever renders badges, not something read off the client. The
/// layout below is the one nitro's <c>GroupBadgePart</c> reads, which is what makes a code
/// written here render in a nitro client without a translation step.
///
/// A code is up to five tokens run together, one per layer, in layer order:
/// <code>
/// base    b + key(2) + colour(2) + position(1)
/// symbol  s + key(2) + colour(2) + position(1)      for keys 1-99
/// symbol  t + (key - 100)(2) + colour(2) + position(1)  for keys 100 and up
/// </code>
/// Key <c>0</c> is an empty layer and contributes nothing, so a badge of a base alone is one
/// token long. The <c>t</c> prefix is the only reason a symbol key may not exceed
/// <see cref="SYMBOL_KEY_MAX"/>: past that the two digits would collide with another part.
/// </summary>
public static class GuildBadgeCodes
{
    public const char BASE_PREFIX = 'b';
    public const char SYMBOL_PREFIX = 's';

    /// <summary>Symbols from <see cref="SYMBOL_ALT_OFFSET"/> up, so their key fits two digits.</summary>
    public const char SYMBOL_ALT_PREFIX = 't';

    public const int SYMBOL_ALT_OFFSET = 100;

    /// <summary>One base and four symbols, which is what the editor draws.</summary>
    public const int MAX_PARTS = 5;

    /// <summary>The nine cells of the 3x3 grid a part may sit in.</summary>
    public const int POSITION_MAX = 8;

    /// <summary>Two digits for the key, and <see cref="SYMBOL_ALT_PREFIX"/> buys one more hundred.</summary>
    public const int SYMBOL_KEY_MAX = (SYMBOL_ALT_OFFSET * 2) - 1;

    public const int BASE_KEY_MAX = 99;
    public const int COLOR_KEY_MAX = 99;

    /// <summary>Prefix, two digits of key, two of colour and one of position.</summary>
    private const int TOKEN_LENGTH = 6;

    /// <summary>
    /// The code for these parts, in order. An empty part (key <c>0</c>) is skipped rather than
    /// written as zeroes, so a half-filled badge and the same badge saved again agree.
    /// </summary>
    public static string Build(IReadOnlyList<GuildBadgePartSnapshot> parts)
    {
        ArgumentNullException.ThrowIfNull(parts);

        var builder = new StringBuilder(parts.Count * 6);

        foreach (var part in parts)
            Append(builder, part);

        return builder.ToString();
    }

    private static void Append(StringBuilder builder, GuildBadgePartSnapshot part)
    {
        if (part.PartId <= 0)
            return;

        var isAlt = part.Type == GuildBadgePartType.Symbol && part.PartId >= SYMBOL_ALT_OFFSET;

        builder.Append(
            part.Type == GuildBadgePartType.Base ? BASE_PREFIX
            : isAlt ? SYMBOL_ALT_PREFIX
            : SYMBOL_PREFIX
        );

        AppendPadded(builder, isAlt ? part.PartId - SYMBOL_ALT_OFFSET : part.PartId);
        AppendPadded(builder, part.ColorId);

        builder.Append(part.Position.ToString(CultureInfo.InvariantCulture));
    }

    private static void AppendPadded(StringBuilder builder, int value)
    {
        if (value < 10)
            builder.Append('0');

        builder.Append(value.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// The parts a code was built from. The edit window reopens on the group's current badge,
    /// and reading them back out of the code is what keeps the group from storing the same
    /// badge twice in two shapes that can disagree.
    ///
    /// A code the server did not write is not worth an exception: it came from a database row an
    /// operator may have typed. Anything that does not parse as a whole token is dropped, so a
    /// damaged code opens the editor on whatever of it was readable.
    /// </summary>
    public static ImmutableArray<GuildBadgePartSnapshot> Parse(string? code)
    {
        if (string.IsNullOrEmpty(code))
            return [];

        var parts = ImmutableArray.CreateBuilder<GuildBadgePartSnapshot>(MAX_PARTS);

        for (var offset = 0; offset + TOKEN_LENGTH <= code.Length; offset += TOKEN_LENGTH)
        {
            if (TryParseToken(code.AsSpan(offset, TOKEN_LENGTH), out var part))
                parts.Add(part);
        }

        return parts.ToImmutable();
    }

    private static bool TryParseToken(
        ReadOnlySpan<char> token,
        [NotNullWhen(true)] out GuildBadgePartSnapshot? part
    )
    {
        part = null;

        var type = token[0] switch
        {
            BASE_PREFIX => GuildBadgePartType.Base,
            SYMBOL_PREFIX or SYMBOL_ALT_PREFIX => GuildBadgePartType.Symbol,
            _ => (GuildBadgePartType?)null,
        };

        if (
            type is not { } partType
            || !int.TryParse(token[1..3], CultureInfo.InvariantCulture, out var key)
            || !int.TryParse(token[3..5], CultureInfo.InvariantCulture, out var colorId)
            || !int.TryParse(token[5..6], CultureInfo.InvariantCulture, out var position)
        )
            return false;

        part = new GuildBadgePartSnapshot
        {
            Type = partType,
            PartId = token[0] == SYMBOL_ALT_PREFIX ? key + SYMBOL_ALT_OFFSET : key,
            ColorId = colorId,
            Position = position,
        };

        return true;
    }
}
