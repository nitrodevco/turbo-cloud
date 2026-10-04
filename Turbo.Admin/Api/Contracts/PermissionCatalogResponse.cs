using System.Collections.Immutable;

namespace Turbo.Admin.Api.Contracts;

/// <summary>Every registered node and meta key.</summary>
public sealed record PermissionCatalogResponse(
    ImmutableArray<PermissionNodeDefinitionView> Nodes,
    ImmutableArray<PermissionMetaKeyView> MetaKeys
);
