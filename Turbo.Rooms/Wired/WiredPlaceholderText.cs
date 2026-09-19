namespace Turbo.Rooms.Wired;

/// <summary>Parsing of the "name" or "name\tdelimiter" string param of the placeholder addons.</summary>
public static class WiredPlaceholderText
{
    private const string DEFAULT_DELIMITER = ", ";

    public static (string name, string delimiter) SplitNameAndDelimiter(string? stringParam)
    {
        if (string.IsNullOrWhiteSpace(stringParam))
            return (string.Empty, DEFAULT_DELIMITER);

        var index = stringParam.IndexOf('\t');

        if (index < 0)
            return (stringParam.Trim(), DEFAULT_DELIMITER);

        var delimiter = stringParam[(index + 1)..];

        return (stringParam[..index].Trim(), delimiter.Length == 0 ? DEFAULT_DELIMITER : delimiter);
    }
}
