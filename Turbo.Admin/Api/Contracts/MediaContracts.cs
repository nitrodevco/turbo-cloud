using System;
using System.Collections.Generic;

namespace Turbo.Admin.Api.Contracts;

/// <summary>
/// What a field stores to show an image, by the shape it takes: a path under the client's image
/// library (<see cref="Relative"/>), a catalog picture's name, a catalog icon's number, one of the
/// client's own images by name, a badge's code, a path in a plugin's <c>wwwroot/</c>, or the full
/// address (<see cref="Url"/>, empty when players load it from no address of its own).
/// </summary>
public sealed record MediaValues(
    string? Relative,
    string? CatalogPicture,
    int? CatalogIcon,
    string? ClientImage,
    string? Badge,
    string? Plugin,
    string Url
);

/// <summary>Where an image is shown: the area, the thing there (its id and title), its field and the panel page that edits it.</summary>
public sealed record MediaUse(string Area, string Id, string Title, string Field, string Link);

/// <summary>
/// One image the hotel can show (<c>docs/media-library.md</c>): <see cref="Id"/> is
/// <c>&lt;source kind&gt;:&lt;path&gt;</c>; <see cref="ThumbUrl"/> draws it from this server, before
/// any publish. <see cref="UsedOn"/> is only given for one image.
/// </summary>
public sealed record MediaImage(
    string Id,
    string Name,
    string Path,
    string Kind,
    string Source,
    string? SourceDetail,
    string Url,
    string ThumbUrl,
    int? Width,
    int? Height,
    long Bytes,
    DateTime? ModifiedUtc,
    IReadOnlyList<string> Tags,
    int UsedCount,
    MediaValues Values,
    IReadOnlyList<MediaUse>? UsedOn = null
);

/// <summary>How many of the found images are of each kind and each source.</summary>
public sealed record MediaFacets(
    IReadOnlyDictionary<string, int> Kinds,
    IReadOnlyDictionary<string, int> Sources
);

/// <summary>A page of the images a search found, of <see cref="Total"/>; <see cref="CanManage"/> says whether the asker may upload, rename and delete.</summary>
public sealed record MediaSearchResponse(
    int Total,
    IReadOnlyList<MediaImage> Items,
    MediaFacets Facets,
    bool CanManage = false
);

/// <summary>A file an upload did not take, and why.</summary>
public sealed record MediaRefusal(string File, string Why);

/// <summary>The images an upload kept, and the files it refused.</summary>
public sealed record MediaUploadResponse(
    IReadOnlyList<MediaImage> Items,
    IReadOnlyList<MediaRefusal> Refused
);

/// <summary>A new name (the file's, its folder kept) and the tags an image is found by; null leaves either as it is.</summary>
public sealed record MediaEditRequest(string? Name, IReadOnlyList<string>? Tags);

/// <summary>Why an image can't be deleted or renamed, with every place that shows it.</summary>
public sealed record MediaInUseResponse(string Message, IReadOnlyList<MediaUse> UsedOn);
