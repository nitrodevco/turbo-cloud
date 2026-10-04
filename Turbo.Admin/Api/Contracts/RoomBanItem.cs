using System;

namespace Turbo.Admin.Api.Contracts;

/// <summary>A player banned from a room, and until when.</summary>
public sealed record RoomBanItem(int PlayerId, string Name, DateTime ExpiresAtUtc);
