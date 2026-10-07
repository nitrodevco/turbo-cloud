namespace Turbo.Primitives.WiredTrading.Enums;

/// <summary>What moved items in or out of a wired chest, as the transaction log shows it.</summary>
public enum WiredTransactionType
{
    Manual = 0,
    Wired = 1,
    ContractPayment = 2,
    ContractReward = 3,
    ContractTrade = 4,
    AutoWithdraw = 5,
}
