namespace Turbo.Assets.Hab;

/// <summary>One file in a <c>.hab</c>, as its manifest lists it.</summary>
public sealed class HabEntry
{
    public string Name { get; init; } = string.Empty;

    public string MimeType { get; init; } = string.Empty;

    /// <summary>Where it starts in the payload.</summary>
    public int Offset { get; init; }

    public int StoredLength { get; init; }

    public int OriginalLength { get; init; }

    /// <summary><c>deflate</c> (zlib) or <c>none</c>.</summary>
    public string Compression { get; init; } = string.Empty;
}
