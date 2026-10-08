using Orleans;

namespace Turbo.Primitives.Sound;

/// <summary>
/// A song as staff write it in the admin panel, before the song directory checks it. The
/// directory trims the text and refuses what it cannot store or play.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record SongDraft
{
    [Id(0)]
    public required string Name { get; init; }

    [Id(1)]
    public required string Author { get; init; }

    /// <summary>The trax track, in the client's format.</summary>
    [Id(2)]
    public required string Track { get; init; }

    [Id(3)]
    public required int LengthSeconds { get; init; }

    /// <summary>The catalog code; empty or null for none.</summary>
    [Id(4)]
    public string? Code { get; init; }

    [Id(5)]
    public required bool IsOfficial { get; init; }
}
