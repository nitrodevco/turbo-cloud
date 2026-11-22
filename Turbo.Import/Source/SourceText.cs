using System.Globalization;

namespace Turbo.Import.Source;

/// <summary>Numbers inside Morningstar's delimited text fields (an item's state, a box's settings).</summary>
internal static class SourceText
{
    /// <summary>A whole number, else 0.</summary>
    public static int Int(string? value) =>
        int.TryParse(
            value?.Trim(),
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var parsed
        )
            ? parsed
            : 0;
}
