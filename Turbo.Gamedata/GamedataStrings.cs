using System.Diagnostics.CodeAnalysis;

namespace Turbo.Gamedata;

/// <summary>Text made fit for the column it goes in.</summary>
internal static class GamedataStrings
{
    /// <summary>
    /// The first <paramref name="max"/> characters, as it is otherwise. Unlike
    /// <see cref="Primitives.Texts.ClientText.Truncate"/> it does not trim: what is cut here is
    /// the server's own text, a key or a summary, kept as it was written.
    /// </summary>
    [return: NotNullIfNotNull(nameof(text))]
    public static string? Truncate(string? text, int max) =>
        text is null || text.Length <= max ? text : text[..max];
}
