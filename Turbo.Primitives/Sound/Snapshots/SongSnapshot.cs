using Orleans;

namespace Turbo.Primitives.Sound.Snapshots;

/// <summary>
/// A trax song: the track the client's sound machine plays, its name and author as the music
/// widgets show them, and how long it runs. Song disks and jukebox playlists name songs by
/// <see cref="Id"/>.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record SongSnapshot
{
    [Id(0)]
    public required int Id { get; init; }

    /// <summary>
    /// The short code an official song is sold under, which the catalog's song disk page can name
    /// instead of the id (<c>GetOfficialSongId</c>). Empty when the song has none.
    /// </summary>
    [Id(1)]
    public required string Code { get; init; }

    [Id(2)]
    public required string Name { get; init; }

    /// <summary>Who made it, as the client prints it ("by ...").</summary>
    [Id(3)]
    public required string Author { get; init; }

    /// <summary>The trax track itself, in the client's format; the server never reads it.</summary>
    [Id(4)]
    public required string Track { get; init; }

    /// <summary>
    /// How long the song plays. The client is sent milliseconds and stops the song at its end,
    /// and the jukebox moves on to the next disk when it is up.
    /// </summary>
    [Id(5)]
    public required int LengthSeconds { get; init; }

    /// <summary>A song staff added for the catalog to sell on disks.</summary>
    [Id(6)]
    public required bool IsOfficial { get; init; }
}
