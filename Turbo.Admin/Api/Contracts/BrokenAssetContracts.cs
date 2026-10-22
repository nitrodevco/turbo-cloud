using System;

namespace Turbo.Admin.Api.Contracts;

/// <summary>
/// Something players can't load, as the broken assets page lists it (<c>BrokenAsset</c>):
/// <c>source</c> is where it was found (<c>client</c>, <c>check</c>, <c>catalog</c>, <c>badge</c>)
/// and <c>key</c> what it is there; <c>kind</c> and <c>name</c> the bundle it is, when known.
/// <c>actions</c> are the fixes that apply (<c>habbo</c>: take it from Habbo, <c>upload</c>,
/// <c>publish</c>); any can be ignored with a note.
/// </summary>
public sealed record BrokenAssetItem(
    string Source,
    string Key,
    string Title,
    string? Kind,
    string? Name,
    string Detail,
    int Count,
    string[] Actions,
    string? Link,
    BrokenAssetIgnored? Ignored
);

/// <summary>Why staff ignored a broken asset, who did and when.</summary>
public sealed record BrokenAssetIgnored(
    string Note,
    int PlayerId,
    string? PlayerName,
    DateTime IgnoredAt
);

/// <summary>
/// A source of broken assets and whether it was read: the client's 404 reports are read only
/// while the client errors plugin is loaded, and <c>note</c> says why one was not.
/// </summary>
public sealed record BrokenAssetSource(string Id, string Title, bool Read, string? Note, int Count);

/// <summary>Every broken asset (ignored ones included, marked) and the sources they came from.</summary>
public sealed record BrokenAssetsResponse(BrokenAssetSource[] Sources, BrokenAssetItem[] Items);

/// <summary>A broken asset to ignore, with why; or, to bring back, without.</summary>
public sealed record BrokenAssetIgnoreInput(string? Source, string? Key, string? Note);
