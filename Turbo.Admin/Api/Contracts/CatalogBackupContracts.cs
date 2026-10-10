using System;

namespace Turbo.Admin.Api.Contracts;

/// <summary>A backup of the catalog as the panel lists it; the name of who took it, when it is known.</summary>
public sealed record CatalogBackupItem(
    int Id,
    string Name,
    int TakenById,
    string? TakenByName,
    DateTime TakenAtUtc,
    bool Automatic,
    int Pages,
    int Offers,
    int Products,
    int FeaturedItems
);

/// <summary>The catalog's backups, the newest first.</summary>
public sealed record CatalogBackupsResponse(CatalogBackupItem[] Items);

/// <summary>A backup to take; a blank name is named after when it is taken.</summary>
public sealed record CatalogBackupRequest(string? Name);
