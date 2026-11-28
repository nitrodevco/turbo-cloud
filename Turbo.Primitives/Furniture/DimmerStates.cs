using System;
using Turbo.Primitives.Rooms.Snapshots.Furniture;
using Turbo.Primitives.Texts;

namespace Turbo.Primitives.Furniture;

/// <summary>
/// Moodlight data as the client's dimmer logic parses it: a comma-joined
/// <c>state,presetId,effectId,#RRGGBB,brightness</c> where state 1 is off and 2 is on. A sixth
/// part, <c>true</c>, says furni keep their own lights; build 116's client reads it (in any
/// case, and only while the moodlight is on) and an older one reads the first five alone.
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

    /// <summary>The parts every moodlight line has; the sixth is optional.</summary>
    public const int FIELD_COUNT = 5;
    public const int KEEP_FURNITURE_LIGHTS_FIELD = 5;
    public const string KEEP_FURNITURE_LIGHTS = "true";

    /// <summary>
    /// The line for <paramref name="preset"/> in <paramref name="state"/>. The sixth part is
    /// written only when the preset keeps furni lights, so every other line is the five parts it
    /// always was.
    /// </summary>
    public static string Compose(int state, DimmerPresetSnapshot preset) =>
        preset.KeepFurnitureLights
            ? string.Join(
                SEPARATOR,
                state,
                preset.Id,
                preset.Type,
                preset.Color,
                preset.Brightness,
                KEEP_FURNITURE_LIGHTS
            )
            : string.Join(
                SEPARATOR,
                state,
                preset.Id,
                preset.Type,
                preset.Color,
                preset.Brightness
            );

    /// <summary>Whether a line's parts say furni keep their own lights, as the client reads it.</summary>
    public static bool KeepsFurnitureLights(ReadOnlySpan<string> fields) =>
        fields.Length > KEEP_FURNITURE_LIGHTS_FIELD
        && string.Equals(
            fields[KEEP_FURNITURE_LIGHTS_FIELD],
            KEEP_FURNITURE_LIGHTS,
            StringComparison.OrdinalIgnoreCase
        );

    public static bool IsValidColor(string color) =>
        color.Length == HexColor.DIGITS + 1 && color[0] == '#' && HexColor.IsRgb(color.AsSpan(1));
}
