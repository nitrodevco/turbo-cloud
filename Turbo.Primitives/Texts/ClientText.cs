namespace Turbo.Primitives.Texts;

/// <summary>
/// Text a client sent, made fit to store. Four places had written this out: the navigator, room
/// settings and both guild grains, and the two guild copies had quietly left out the trim, so a
/// group could be named with leading spaces where a room could not.
/// </summary>
public static class ClientText
{
    /// <summary>
    /// Trimmed, and cut to <paramref name="maxLength"/>. Null and whitespace become empty, so a
    /// caller that refuses empty input gets one check rather than two.
    /// </summary>
    public static string Truncate(string? value, int maxLength)
    {
        value = value?.Trim() ?? string.Empty;

        return value.Length <= maxLength ? value : value[..maxLength];
    }
}
