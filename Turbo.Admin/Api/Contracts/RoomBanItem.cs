using System;

namespace Turbo.Admin.Api.Contracts;

/// <summary>A player banned from a room, with their figure, and until when.</summary>
public sealed record RoomBanItem(int PlayerId, string Name, string Figure, DateTime ExpiresAtUtc);
