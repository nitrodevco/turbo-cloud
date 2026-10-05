namespace Turbo.Admin.Api.Contracts;

/// <summary>A player to mute in a room, for some minutes.</summary>
public sealed record RoomMuteRequest(int PlayerId, int Minutes);
