using System;

namespace Turbo.Database.Extensions;

/// <summary>
/// Patterns for <c>EF.Functions.Like</c> over text people type. PostgreSQL compares text exactly,
/// so a search that ignores case lowers both sides: the column with <c>ToLower()</c> and the text
/// here. <c>%</c>, <c>_</c> and the escape character in the text match themselves.
/// <code>EF.Functions.Like(x.Name.ToLower(), LikePatterns.Containing(text), LikePatterns.ESCAPE)</code>
/// A trigram index on <c>lower(column)</c> serves <see cref="Containing"/>; a b-tree on the column
/// serves <see cref="StartingWith"/> with the <c>C</c> collation or <c>text_pattern_ops</c>.
/// </summary>
public static class LikePatterns
{
    /// <summary>The escape character the patterns use; PostgreSQL's default.</summary>
    public const string ESCAPE = "\\";

    /// <summary>The text anywhere, lowered.</summary>
    public static string Containing(string text) => "%" + Escape(text.ToLowerInvariant()) + "%";

    /// <summary>The text at the start, lowered.</summary>
    public static string StartingWith(string text) => Escape(text.ToLowerInvariant()) + "%";

    /// <summary>The text with <c>%</c>, <c>_</c> and the escape character escaped.</summary>
    public static string Escape(string text) =>
        text.Replace(ESCAPE, ESCAPE + ESCAPE, StringComparison.Ordinal)
            .Replace("%", ESCAPE + "%", StringComparison.Ordinal)
            .Replace("_", ESCAPE + "_", StringComparison.Ordinal);
}
