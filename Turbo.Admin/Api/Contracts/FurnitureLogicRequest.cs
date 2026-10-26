namespace Turbo.Admin.Api.Contracts;

/// <summary>
/// Definitions to give a logic: <c>logic</c> to each of <c>ids</c>; without a logic, each its
/// matching one; without ids, every definition whose logic does not match.
/// </summary>
public sealed record FurnitureLogicRequest(int[]? Ids, string? Logic);
