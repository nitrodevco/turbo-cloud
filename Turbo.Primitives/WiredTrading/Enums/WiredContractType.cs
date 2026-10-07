namespace Turbo.Primitives.WiredTrading.Enums;

/// <summary>
/// The kind of wired contract, sent as a short: the first three constants of the client's
/// contract contents parser, each opening its own editor (<c>PaymentContract</c>,
/// <c>TradeContract</c>, <c>RewardContract</c>).
/// </summary>
public enum WiredContractType : short
{
    /// <summary>The user pays into the room; followed by the payment mode, receive text and layout.</summary>
    Payment = 0,

    /// <summary>A give-for-get trade; the rules definition alone.</summary>
    Trade = 1,

    /// <summary>The user is given something; followed by the earnings category, dialog flag and text.</summary>
    Reward = 2,
}
