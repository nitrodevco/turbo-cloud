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

    public static IWiredParamRule HandItem(WiredConfig config) =>
        new WiredRangeParamRule(0, config.MaxHandItemId, 0);

    public static IWiredParamRule Effect(WiredConfig config) =>
        new WiredRangeParamRule(0, config.MaxEffectId, 0);

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
