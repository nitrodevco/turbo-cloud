namespace Turbo.Admin.Api.Contracts;

/// <summary>
/// A song as the editor saves it: its name, author, trax track and length in seconds, whether it
/// is official, and an optional catalog code, which may not begin with a digit.
/// </summary>
public sealed record SongRequest(
    string? Name,
    string? Author,
    string? Track,
    int Length,
    bool Official,
    string? Code
);
