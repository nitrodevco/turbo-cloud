using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Players;

namespace Turbo.Primitives.Plugins;

/// <summary>Who a theme reaches (<c>docs/plugin-data.md</c>, "Who gets which theme").</summary>
public enum ThemeStatus
{
    /// <summary>Players get it when it is the hotel's default, or when they choose it and it may be chosen.</summary>
    Live = 0,

    /// <summary>Only its audience gets it, and staff who may preview themes when they choose it.</summary>
    Development = 1,

    /// <summary>Nobody gets it, but as the theme a theme that reaches them extends.</summary>
    Off = 2,
}

/// <summary>
/// A theme and who gets it: its status, whether it is the hotel's default (one at most, and Live),
/// whether players may choose it, and, while it is in development, the players and permission
/// groups that get it. A theme no one set is Live, not the default and not to be chosen.
/// </summary>
public sealed record ThemeAudience(
    string Key,
    string? Extends,
    ThemeStatus Status,
    bool IsDefault,
    bool Selectable,
    ImmutableArray<int> Players,
    ImmutableArray<string> Groups
);

/// <summary>What staff change of a theme's audience; a null field is left as it is.</summary>
public sealed record ThemeAudienceChange(
    ThemeStatus? Status = null,
    bool? IsDefault = null,
    bool? Selectable = null,
    ImmutableArray<int>? Players = null,
    ImmutableArray<string>? Groups = null
);

/// <summary>A theme a player may choose: its key and the name to show.</summary>
public sealed record ThemeChoice(string Key, string Name);

/// <summary>
/// The theme a session draws with (<see cref="Theme"/>, empty for the hotel's default) and the merged
/// variables of its chain (<c>templates.patches</c>, <c>asset.overlays</c>, ...) when they are not
/// the ones every client loads already (null when they are); the themes the player may choose and
/// the one they chose (empty: the hotel's default).
/// </summary>
public sealed record ThemeSession(
    string Theme,
    IReadOnlyDictionary<string, string>? Variables,
    ImmutableArray<ThemeChoice> Choices,
    string Chosen
);

/// <summary>
/// Which theme each player gets (<c>docs/plugin-data.md</c>, "Who gets which theme"): a development
/// theme's look reaches only its audience, everyone else draws the default theme's chain, or the
/// theme they chose. The external variables every client loads are the default's; a session's own
/// are its theme's chain, built once per theme and kept until a theme or the hotel changes.
/// </summary>
public interface IThemeAudiences
{
    /// <summary>Every theme the hotel has, with who gets it.</summary>
    public Task<ImmutableArray<ThemeAudience>> ListAsync(CancellationToken ct);

    /// <summary>
    /// Changes who gets a theme; throws <see cref="System.ArgumentException"/> naming why for a key
    /// that is no theme, a default that is not Live, or a player id or group that is not there.
    /// Making one the default makes the last one not.
    /// </summary>
    public Task<ThemeAudience> SetAsync(
        string key,
        ThemeAudienceChange change,
        PlayerId actor,
        CancellationToken ct
    );

    /// <summary>
    /// The theme the player draws with: one in development whose audience they are in, the one they
    /// chose while they may, else the default. <paramref name="requested"/> (staff's "Preview as")
    /// is honoured once, for this load, when the player may have it.
    /// </summary>
    public Task<ThemeSession> SessionAsync(
        PlayerId player,
        string? requested,
        CancellationToken ct
    );

    /// <summary>
    /// Keeps the player's choice (empty: the hotel's default); throws
    /// <see cref="System.ArgumentException"/> for a theme they may not choose.
    /// </summary>
    public Task ChooseAsync(PlayerId player, string key, CancellationToken ct);
}
