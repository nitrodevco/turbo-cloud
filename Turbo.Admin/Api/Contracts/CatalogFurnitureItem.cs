namespace Turbo.Admin.Api.Contracts;

/// <summary>A furniture definition an offer can give: its class name, sprite and floor or wall.</summary>
public sealed record CatalogFurnitureItem(int Id, string Name, int SpriteId, string Type);
