using System;
using System.Collections.Generic;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Snapshots;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Primitives.Players;

/// <summary>
/// Works out the look a player is shown with from their saved figure and an optional temporary
/// override. Pure: nothing here reads or writes state, so it is the one place the layering is
/// decided and the player grain only calls it.
/// </summary>
public static class PlayerLook
{
    private const char PART_SEPARATOR = '.';
    private const char FIELD_SEPARATOR = '-';

    /// <summary>
    /// The figure and gender to show. With no override that is the saved pair; otherwise the
    /// override figure (whole, or merged part by part over the saved one) and the override's
    /// gender where it names one.
    /// </summary>
    public static (string Figure, AvatarGenderType Gender) Resolve(
        string savedFigure,
        AvatarGenderType savedGender,
        PlayerLookOverrideSnapshot? lookOverride
    )
    {
        if (lookOverride is null)
            return (savedFigure, savedGender);

        var figure = lookOverride.Mode switch
        {
            LookOverrideMode.Replace => lookOverride.Figure,
            LookOverrideMode.MergeParts => MergeParts(savedFigure, lookOverride.Figure),
            _ => throw new ArgumentOutOfRangeException(
                nameof(lookOverride),
                lookOverride.Mode,
                "Unknown look override mode."
            ),
        };

        return (figure, lookOverride.Gender ?? savedGender);
    }

    /// <summary>
    /// Lays <paramref name="overlayFigure"/> over <paramref name="baseFigure"/>: a part of the
    /// overlay replaces the base part of the same set type in place, a base part the overlay
    /// does not name is kept, and an overlay part of a set the base lacks is added at the end.
    /// Empty parts are dropped; a set named twice keeps its last part.
    /// </summary>
    public static string MergeParts(string baseFigure, string overlayFigure)
    {
        var overlay = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var part in Split(overlayFigure))
            overlay[SetTypeOf(part)] = part;

        var merged = new List<string>();
        var placed = new HashSet<string>(StringComparer.Ordinal);
        foreach (var part in Split(baseFigure))
        {
            var setType = SetTypeOf(part);
            if (!placed.Add(setType))
                continue;

            merged.Add(overlay.TryGetValue(setType, out var replacement) ? replacement : part);
        }

        foreach (var (setType, part) in overlay)
        {
            if (placed.Add(setType))
                merged.Add(part);
        }

        return string.Join(PART_SEPARATOR, merged);
    }

    private static string SetTypeOf(string part)
    {
        var end = part.IndexOf(FIELD_SEPARATOR);
        return end < 0 ? part : part[..end];
    }

    private static string[] Split(string figure) =>
        figure.Split(PART_SEPARATOR, StringSplitOptions.RemoveEmptyEntries);
}
