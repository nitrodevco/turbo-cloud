namespace Turbo.Admin.Api.Contracts;

/// <summary>A badge a player owns, and the slot they wear it in; null when they do not wear it.</summary>
public sealed record PlayerBadgeItem(string Code, int? Slot);
