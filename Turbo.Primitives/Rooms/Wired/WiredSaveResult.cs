using Orleans;

namespace Turbo.Primitives.Rooms.Wired;

/// <summary>
/// How a wired box's save went. A refused save carries the hotel text the editor shows
/// (<c>wiredfurni.error.*</c>) when the box gave one.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record WiredSaveResult
{
    public static readonly WiredSaveResult Saved = new() { IsSaved = true };

    public static readonly WiredSaveResult Refused = new() { IsSaved = false };

    [Id(0)]
    public required bool IsSaved { get; init; }

    /// <summary>The text key the editor shows for a refusal; null for the generic one.</summary>
    [Id(1)]
    public string? ErrorKey { get; init; }

    public static WiredSaveResult RefusedWith(string errorKey) =>
        new() { IsSaved = false, ErrorKey = errorKey };
}
