namespace Turbo.Admin.Api.Contracts;

/// <summary>One of a player's balances: credits, duckets, diamonds, a points type.</summary>
public sealed record PlayerCurrencyItem(int TypeId, string Name, int Amount);
