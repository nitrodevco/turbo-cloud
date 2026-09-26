using System;
using Turbo.Primitives.Texts;

namespace Turbo.Primitives.Furniture;

/// <summary>
/// Moodlight data as the client's dimmer logic parses it: a comma-joined
/// <c>state,presetId,effectId,#RRGGBB,brightness</c> where state 1 is off and 2 is on.
/// </summary>
public static class DimmerStates
{
    public const int OFF = 1;
    public const int ON = 2;
    public const int EFFECT_ROOM = 1;
    public const int EFFECT_BACKGROUND = 2;
    public const int PRESET_COUNT = 3;
    public const int MIN_BRIGHTNESS = 76;
    public const int MAX_BRIGHTNESS = 255;
    public const string DEFAULT_COLOR = "#000000";
    public const char SEPARATOR = ',';
    public const int FIELD_COUNT = 5;

    public static bool IsValidColor(string color) =>
        color.Length == HexColor.DIGITS + 1 && color[0] == '#' && HexColor.IsRgb(color.AsSpan(1));
}
