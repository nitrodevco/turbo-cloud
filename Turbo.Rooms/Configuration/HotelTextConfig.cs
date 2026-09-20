namespace Turbo.Rooms.Configuration;

/// <summary>
/// Where the hotel's texts are read from. They are the client's own file, so the path is a
/// hotel setting and ships empty: with no path the server simply has no texts and the places
/// that would show a name show the plain number instead.
/// </summary>
public class HotelTextConfig
{
    public const string SECTION_NAME = "Turbo:Texts";

    /// <summary>
    /// Where the client's <c>ExternalTexts.json</c> is: a path on disk, or an <c>http</c> or
    /// <c>https</c> address beside the rest of the hotel's assets. Empty runs without texts.
    /// </summary>
    public string ExternalTextsPath { get; init; } = string.Empty;

    /// <summary>How deep a <c>${key}</c> chain is followed before it is treated as a loop.</summary>
    public int MaxKeyDepth { get; init; } = 8;

    /// <summary>How long to wait on the address before giving up and starting without texts.</summary>
    public int FetchTimeoutSeconds { get; init; } = 30;
}
