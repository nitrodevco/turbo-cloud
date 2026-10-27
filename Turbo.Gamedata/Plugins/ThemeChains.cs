using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Turbo.Primitives.Plugins;

namespace Turbo.Gamedata.Plugins;

/// <summary>
/// Which themes' look a client is given (<see cref="IThemeAudiences"/>), worked out from the themes
/// and who gets each: XenForo's styles, where a style is its parent's with its own changes over
/// them, one is the default, and a visitor may pick one that is user selectable.
/// <list type="bullet">
/// <item>A theme's <b>chain</b> is the theme and every theme it extends, up to Habbo's own.</item>
/// <item>A client drawing a theme gets the parts of its chain's themes and of every plugin that is
/// no theme; with no default set and no theme of its own, every Live theme's, as before themes had
/// an audience.</item>
/// <item>A session's theme is a development theme the player chose and may have, else a development
/// theme whose audience they are in, else the theme they chose while it may be chosen, else the
/// default.</item>
/// </list>
/// </summary>
internal static class ThemeChains
{
    /// <summary>The hotel's own theme: staff's edits, which extends Habbo's own with no file.</summary>
    public const string HOTEL = IHotelDataPlugin.KEY;

    private const int MAX_DEPTH = 8;

    /// <summary>The default theme: the one marked so while it is Live; null for none.</summary>
    public static string? DefaultOf(IEnumerable<ThemeAudience> themes) =>
        themes.FirstOrDefault(x => x.IsDefault && x.Status == ThemeStatus.Live)?.Key;

    /// <summary>The theme and every theme it extends that the hotel has.</summary>
    public static ImmutableHashSet<string> ChainOf(
        string theme,
        IReadOnlyDictionary<string, ThemeAudience> themes
    )
    {
        var chain = ImmutableHashSet.CreateBuilder<string>(StringComparer.OrdinalIgnoreCase);
        string? key = theme;

        while (key is not null && chain.Count < MAX_DEPTH && chain.Add(key))
            key = themes.TryGetValue(key, out var at) ? at.Extends : null;

        return chain.ToImmutable();
    }

    /// <summary>
    /// Whether a client drawing <paramref name="theme"/> (null: the hotel's look with no theme of
    /// its own) is given the parts of a plugin, the hotel's own included.
    /// </summary>
    public static Func<string, bool> Includes(
        string? theme,
        IReadOnlyDictionary<string, ThemeAudience> themes
    )
    {
        if (theme is null)
            return plugin =>
                !themes.TryGetValue(plugin, out var at) || at.Status == ThemeStatus.Live;

        var chain = ChainOf(theme, themes);

        return plugin => !themes.ContainsKey(plugin) || chain.Contains(plugin);
    }

    /// <summary>Whether the player is in a theme's development audience.</summary>
    public static bool InAudience(ThemeAudience theme, int player, IReadOnlySet<string> groups) =>
        theme.Players.Contains(player) || theme.Groups.Any(groups.Contains);

    /// <summary>
    /// Whether the player may choose a theme: a Live one that may be chosen (or the default), a
    /// development one whose audience they are in or when they may preview themes.
    /// </summary>
    public static bool MayChoose(
        ThemeAudience theme,
        int player,
        IReadOnlySet<string> groups,
        bool mayPreview
    ) =>
        theme.Status switch
        {
            ThemeStatus.Live => theme.Selectable || theme.IsDefault,
            ThemeStatus.Development => mayPreview || InAudience(theme, player, groups),
            _ => false,
        };

    /// <summary>
    /// The theme a session draws (null: the hotel's look with no theme of its own, when there is no
    /// default). <paramref name="requested"/> is staff's "Preview as" for this load: honoured for any
    /// theme that is not Off when they may preview themes, else as a choice is.
    /// </summary>
    public static string? Resolve(
        IReadOnlyList<ThemeAudience> themes,
        int player,
        IReadOnlySet<string> groups,
        bool mayPreview,
        string? chosen,
        string? requested
    )
    {
        ThemeAudience? Find(string? key) =>
            string.IsNullOrEmpty(key)
                ? null
                : themes.FirstOrDefault(x =>
                    string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase)
                );

        if (
            Find(requested) is { } preview
            && (
                mayPreview
                    ? preview.Status != ThemeStatus.Off
                    : MayChoose(preview, player, groups, mayPreview)
            )
        )
            return preview.Key;

        var choice =
            Find(chosen) is { } own && MayChoose(own, player, groups, mayPreview) ? own : null;

        if (choice is { Status: ThemeStatus.Development })
            return choice.Key;

        if (
            themes
                .Where(x => x.Status == ThemeStatus.Development && InAudience(x, player, groups))
                .OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault() is
            { } audience
        )
            return audience.Key;

        return choice?.Key ?? DefaultOf(themes);
    }
}
