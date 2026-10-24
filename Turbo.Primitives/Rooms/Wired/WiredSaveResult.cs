using System.Collections.Generic;
using Orleans;

namespace Turbo.Primitives.Rooms.Wired;

/// <summary>
/// How a wired box's save went. A refused save carries the hotel text the editor shows
/// (<c>wiredfurni.error.*</c>) when the box gave one, or the server error code Habbo answers a
/// save it has no text for with.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record WiredSaveResult
{
    public static readonly WiredSaveResult Saved = new() { IsSaved = true };

    public static readonly WiredSaveResult Refused = new() { IsSaved = false };

    /// <summary>
    /// A refusal answered as Habbo does one it has no text for: <c>ErrorReport</c> with error
    /// code 1, which the client shows as "Sorry, received server error: 1" (official client
    /// capture 2026-10-24: a 21-pick Chase and Join Team team 0 or 5, each stored nothing).
    /// </summary>
    public static readonly WiredSaveResult ServerError = new()
    {
        IsSaved = false,
        ServerErrorCode = 1,
    };

    [Id(0)]
    public required bool IsSaved { get; init; }

    /// <summary>The text key the editor shows for a refusal; null for the generic one.</summary>
    [Id(1)]
    public string? ErrorKey { get; init; }

    /// <summary>What fills the error text's <c>%name%</c> slots, in order.</summary>
    [Id(2)]
    public List<(string Key, string Value)> Parameters { get; init; } = [];

    /// <summary>The <c>ErrorReport</c> code to answer with instead of a text; null for a text.</summary>
    [Id(3)]
    public int? ServerErrorCode { get; init; }

    public static WiredSaveResult RefusedWith(
        string errorKey,
        IEnumerable<(string Key, string Value)>? parameters = null
    ) =>
        new()
        {
            IsSaved = false,
            ErrorKey = errorKey,
            Parameters = [.. parameters ?? []],
        };
}
