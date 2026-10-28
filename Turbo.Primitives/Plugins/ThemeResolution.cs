using System.Collections.Generic;
using Turbo.Primitives.Players;

namespace Turbo.Primitives.Plugins;

/// <summary>The hotel's default theme and each requested player's currently permitted theme.</summary>
public sealed record ThemeResolution(
    string? Default,
    IReadOnlyList<(PlayerId Player, string? Theme)> Players
);
