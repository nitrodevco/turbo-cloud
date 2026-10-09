using System;

namespace Turbo.Admin.Api.Contracts;

/// <summary>When a catalog page runs out (UTC), and the image the client keeps for it.</summary>
public sealed record ExpiringPageRequest(DateTime? ExpiresAt, string? Image);
