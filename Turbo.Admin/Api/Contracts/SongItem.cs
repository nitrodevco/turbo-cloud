namespace Turbo.Admin.Api.Contracts;

/// <summary>
/// A trax song as the song list shows it. <see cref="Length"/> is in seconds;
/// <see cref="Discs"/> is how many song disks carry it; <see cref="Code"/> is the catalog code
/// an official song can be sold under instead of its id, or null.
/// </summary>
public sealed record SongItem(
    int Id,
    string Name,
    string Author,
    int Length,
    bool Official,
    int Discs,
    string? Code
);
