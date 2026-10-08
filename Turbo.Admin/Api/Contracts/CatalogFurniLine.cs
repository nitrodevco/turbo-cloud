namespace Turbo.Admin.Api.Contracts;

/// <summary>A furni line (furnidata <c>furniline</c>) and how many furniture definitions are in it.</summary>
public sealed record CatalogFurniLine(string Line, int Count);
