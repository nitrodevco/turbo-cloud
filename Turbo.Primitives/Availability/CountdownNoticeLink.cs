using System;

namespace Turbo.Primitives.Availability;

/// <summary>
/// Where a countdown notice goes when it is clicked: a web address, opened in the browser
/// (<c>notifications.new_feature.external_link</c>), or one of the client's own links such as
/// <c>habbopages/...</c> (<c>internal_link</c>). Anything else, a <c>javascript:</c> address say, is
/// no link at all.
/// </summary>
public static class CountdownNoticeLink
{
    /// <summary>The longest link a notice carries.</summary>
    public const int MAX_LENGTH = 512;

    public const string EXTERNAL = "external_link";
    public const string INTERNAL = "internal_link";

    /// <summary>
    /// The variable the link is written to (<see cref="EXTERNAL"/> or <see cref="INTERNAL"/>), and
    /// the link trimmed; false for an empty one and for one that is neither.
    /// </summary>
    public static bool TryRead(string? link, out string variable, out string value)
    {
        value = link?.Trim() ?? string.Empty;
        variable = string.Empty;

        if (value.Length == 0 || value.Length > MAX_LENGTH || value.AsSpan().ContainsAny("\r\n"))
            return false;

        if (
            value.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
        )
        {
            variable = EXTERNAL;
            return true;
        }

        // A client link names no scheme: a colon would make it an address of some other kind.
        if (value.Contains(':', StringComparison.Ordinal) || value.StartsWith('/'))
            return false;

        variable = INTERNAL;
        return true;
    }
}
