using Turbo.Primitives.Settings;
using Turbo.Primitives.Trading.Enums;

namespace Turbo.Players.Configuration;

/// <summary>
/// The trading pass: who may trade and sell on the marketplace. Habbo asks for it by default; a
/// hotel can let players without it receive only, or ask for no pass at all. Changes apply at
/// once, from the next trade opened or offer made.
/// </summary>
public class TradingConfig
{
    public const string SECTION_NAME = "Turbo:Trading";

    /// <summary>
    /// What a player without the pass may do. <c>Required</c> (Habbo): starting a trade without
    /// it shows "You're not a Habbo Citizen!", and asking someone without it to trade answers
    /// "doesn't want to / cannot trade atm"; neither may sell on the marketplace.
    /// <c>ReceiveOnly</c>: the trade opens and they can take items but not give any.
    /// <c>Off</c>: everyone trades.
    /// </summary>
    [LiveSetting]
    public TradingPassMode PassMode { get; init; } = TradingPassMode.Required;

    /// <summary>
    /// The badge that is the pass: whoever owns it holds the pass, however it came to them. Habbo
    /// gives <c>ACH_TraderPass1</c> as a prize of the Introduction reward track, so changing
    /// where it is earned is a matter of which reward track step, achievement level or catalogue
    /// offer gives this badge. Players who own it already keep it.
    /// </summary>
    [LiveSetting]
    public string PassBadgeCode { get; init; } = "ACH_TraderPass1";
}
