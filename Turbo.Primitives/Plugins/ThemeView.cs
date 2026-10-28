using System.Collections.Generic;

namespace Turbo.Primitives.Plugins;

/// <summary>
/// One materialized theme: its revisioned look and the variables to supply during session
/// initialization. Session variables contain only the merged entries, without the look's text
/// URL, and are null when the default file already supplies them. Both describe the same build.
/// </summary>
public sealed record ThemeView(
    ThemeLook Look,
    IReadOnlyDictionary<string, string>? SessionVariables
);
