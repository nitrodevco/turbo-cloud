namespace Turbo.Admin.Api.Contracts;

/// <summary>The player, by name, a room is given to.</summary>
public sealed record RoomOwnerRequest(string? Name);
