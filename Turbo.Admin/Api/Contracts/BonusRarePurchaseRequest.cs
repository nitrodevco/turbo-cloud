namespace Turbo.Admin.Api.Contracts;

/// <summary>Credits a player bought, under the purchase's own reference (an order number).</summary>
public sealed record BonusRarePurchaseRequest(int? PlayerId, int? Credits, string? Reference);
