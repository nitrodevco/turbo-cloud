namespace Turbo.Admin.Api.Contracts;

/// <summary>A registered node.</summary>
public sealed record PermissionNodeDefinitionView(
    string Node,
    string Description,
    int? ClientLevel,
    bool GrantedByDefault
);
