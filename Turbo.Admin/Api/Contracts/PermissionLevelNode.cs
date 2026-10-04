namespace Turbo.Admin.Api.Contracts;

/// <summary>A registered node the client offers at a level, though the server refuses it.</summary>
public sealed record PermissionLevelNode(string Node, string Description, int ClientLevel);
