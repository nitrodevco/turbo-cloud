namespace Turbo.Admin.Api.Contracts;

/// <summary>Where a page goes: under which page, and at which place among its children.</summary>
public sealed record CatalogMoveRequest(int ParentId, int Index);
