namespace Turbo.Admin.Api.Contracts;

/// <summary>The figure sets to give a player, or take from them.</summary>
public sealed record ClothingGrantRequest(int[]? SetIds);
