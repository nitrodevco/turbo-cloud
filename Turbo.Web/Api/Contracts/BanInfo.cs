using System;

namespace Turbo.Web.Api.Contracts;

/// <summary>A ban in force on the signed-in player: why, and until when (null for good).</summary>
public sealed record BanInfo(string Reason, DateTime? ExpiresAtUtc);
