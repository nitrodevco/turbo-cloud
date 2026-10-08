namespace Turbo.Admin.Api.Contracts;

/// <summary>One song with its track, for the editor; the fields are <see cref="SongItem"/>'s.</summary>
public sealed record SongDetail(
    int Id,
    string Name,
    string Author,
    int Length,
    bool Official,
    int Discs,
    string? Code,
    string Track
);
