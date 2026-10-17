namespace Turbo.Admin.Api.Contracts;

/// <summary>A player named on a room's page, with their figure for the panel to draw them by.</summary>
public sealed record RoomPlayerRef(int Id, string Name, string Figure);
