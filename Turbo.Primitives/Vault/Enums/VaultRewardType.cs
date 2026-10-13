namespace Turbo.Primitives.Vault.Enums;

/// <summary>
/// What an earnings row pays. Flash's <c>EarningsView.onIncomeRewardDataReceived</c>
/// (catalog/vault/EarningsView.as:96) adds type 0 into a category's DucketValue and type 1 into
/// its CreditValue, and counts any row with a product code; the official client capture's daily
/// gift and achievement rows (duckets) are all type 0.
/// </summary>
public enum VaultRewardType
{
    Duckets = 0,
    Credits = 1,
    Product = 2,
}
