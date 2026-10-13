namespace Turbo.Primitives.Moderation;

/// <summary>
/// The mod action window's action types (<c>ModActionCtrl.as</c>, the third argument of each
/// <c>ModActionDefinition</c>): 1 alert, 2 mute, 3 ban, 4 kick, 5 trade lock. 6 (message) is
/// <c>ModMessage</c>, which is not a sanction.
/// </summary>
public enum ModSanctionType
{
    Caution = 1,
    Mute = 2,
    Ban = 3,
    Kick = 4,
    TradeLock = 5,
}

/// <summary>
/// One sanction from the mod action window. <see cref="BanTypeId"/> is the ban's sanction type
/// (<c>ModBan</c>'s fourth argument), <see cref="Minutes"/> a trade lock's length
/// (<c>ModTradingLock</c>'s third).
/// </summary>
public sealed record ModSanction(
    ModSanctionType Type,
    string Message,
    int BanTypeId = 0,
    int Minutes = 0
);
