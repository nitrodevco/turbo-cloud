using Orleans;
using Turbo.Primitives.Sound.Snapshots;

namespace Turbo.Primitives.Sound;

/// <summary>
/// The answer to a song edit: saved, with the song as it now stands (null after a delete); or
/// refused, with why, in words for the editor.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record SongEditResult
{
    [Id(0)]
    public required bool Saved { get; init; }

    [Id(1)]
    public SongSnapshot? Song { get; init; }

    [Id(2)]
    public string? Error { get; init; }

    /// <summary>Whether the song did not exist, as opposed to being refused for what was asked.</summary>
    [Id(3)]
    public bool NotFound { get; init; }

    public static SongEditResult Done(SongSnapshot? song) => new() { Saved = true, Song = song };

    public static SongEditResult Refused(string error) => new() { Saved = false, Error = error };

    public static SongEditResult Missing() =>
        new()
        {
            Saved = false,
            Error = "That song is gone.",
            NotFound = true,
        };
}
