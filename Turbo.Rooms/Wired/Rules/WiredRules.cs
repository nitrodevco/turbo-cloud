using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Configuration;

namespace Turbo.Rooms.Wired.Rules;

/// <summary>
/// The int param rules several boxes share. Every int a client saves into a box passes a rule
/// before it is stored, so a box should pick the narrowest rule that fits; <see cref="AnyInt"/>
/// is only for values that are genuinely unbounded.
/// </summary>
internal static class WiredRules
{
    /// <summary>What a variable hangs on. "None" is never a saved choice.</summary>
    public static IWiredParamRule VariableTarget(WiredVariableTargetType defaultTarget) =>
        new WiredEnumParamRule<WiredVariableTargetType>(
            defaultTarget,
            WiredVariableTargetType.Furni,
            WiredVariableTargetType.User,
            WiredVariableTargetType.Global,
            WiredVariableTargetType.Context
        );

    /// <summary>
    /// A hand item; one past the hotel's last is refused with "Hand item type is not allowed!"
    /// (<c>wiredfurni.error.invalid_handitem_type</c>). Inference: the editors offer a fixed list
    /// plus any captured hand item (<c>ensureOptionExists</c>, actiontypes/_-M2V.as:108,
    /// AS3 WIN63-202610131318), so the server's bound is the only one there is.
    /// </summary>
    public static IWiredParamRule HandItem(WiredConfig config) =>
        // A fresh hand item box (selector, condition, bot) holds item 2 on the official server
        // (2026-10-20T17-33-23 / 17-37-19 / 17-39-35-Open-wait).
        new WiredRangeParamRule(0, config.MaxHandItemId, 2)
        {
            ErrorKey = "wiredfurni.error.invalid_handitem_type",
        };

    /// <summary>A tile coordinate or an offset between two tiles, either direction.</summary>
    public static IWiredParamRule TileOffset(WiredConfig config) =>
        new WiredRangeParamRule(-config.MaxCoordinate, config.MaxCoordinate, 0);

    /// <summary>Counts, unix seconds and other values that are never negative.</summary>
    public static IWiredParamRule NonNegative(int defaultValue = 0) =>
        new WiredRangeParamRule(0, int.MaxValue, defaultValue);

    /// <summary>
    /// No bound: one half of a 64-bit value, a bit mask, or a variable's value. Code reading it
    /// must cope with any int.
    /// </summary>
    public static IWiredParamRule AnyInt(int defaultValue = 0) => new WiredParamRule(defaultValue);
}
