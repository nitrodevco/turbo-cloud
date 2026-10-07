namespace Turbo.Primitives.WiredTrading.Enums;

/// <summary>
/// What a payment contract accepts, sent as a short: the radio group of the client's
/// <c>PaymentContract</c> (<c>wiredcontracts.payment_contract.mode.&lt;n&gt;</c>), whose
/// requirements editor is enabled only for <see cref="Specific"/>.
/// </summary>
public enum WiredContractPaymentMode : short
{
    /// <summary>"Anything (donation)": whatever the user chooses to give.</summary>
    Donation = 0,

    /// <summary>"Specific payment": one of the contract's give rules.</summary>
    Specific = 1,
}
