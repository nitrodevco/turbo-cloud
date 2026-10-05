namespace Turbo.Admin.Api.Contracts;

/// <summary>A player to ban from a room, by id or by name, for an hour, a day or for good (<c>Hour</c>, <c>Day</c>, <c>Permanent</c>).</summary>
public sealed record RoomBanRequest(int? PlayerId, string? Name, string? Duration);
