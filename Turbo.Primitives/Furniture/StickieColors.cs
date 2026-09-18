using System;
using System.Collections.Immutable;

namespace Turbo.Primitives.Furniture;

/// <summary>
/// The post-it palette the client's <c>FurnitureStickieLogic</c> recognises. A stickie's legacy
/// data is <c>"&lt;colour&gt; &lt;text&gt;"</c>: the colour hex, one space, then the note. An unknown
/// colour renders as <see cref="DEFAULT"/> on the client, so the server rejects it outright.
/// </summary>
public static class StickieColors
{
    public const string DEFAULT = "FFFF33";
    public const char SEPARATOR = ' ';

    public static readonly ImmutableArray<string> All =
    [
        "9CCEFF",
        "FF9CFF",
        "9CFF9C",
        "FFFF33",
        "FFFFFF",
        "FF9C9C",
        "FFCC66",
        "9CFFFF",
    ];

    public static bool IsValid(string color) =>
        All.Contains(color, StringComparer.OrdinalIgnoreCase);

    public static string Compose(string color, string text) =>
        string.IsNullOrEmpty(text) ? color : $"{color}{SEPARATOR}{text}";

    public static (string Color, string Text) Split(string data)
    {
        if (string.IsNullOrEmpty(data))
            return (DEFAULT, string.Empty);

        var separator = data.IndexOf(SEPARATOR);

        return separator <= 0 ? (data, string.Empty) : (data[..separator], data[(separator + 1)..]);
    }
}
