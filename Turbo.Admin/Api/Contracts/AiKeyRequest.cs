namespace Turbo.Admin.Api.Contracts;

/// <summary>A provider's new key; blank removes it. Never sent back.</summary>
public sealed record AiKeyRequest(string? Key);
