using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Catalog;

/// <summary>
/// Tells a client the catalog changed: it drops what it has and asks again, with an alert in an
/// open catalog or a notification otherwise. <see cref="NewFurniDataHash"/>, when set, makes it
/// load the furnidata again too.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record CatalogPublishedMessageComposer : IComposer
{
    [Id(0)]
    public bool InstantlyRefreshCatalogue { get; init; } = true;

    [Id(1)]
    public string? NewFurniDataHash { get; init; }
}
