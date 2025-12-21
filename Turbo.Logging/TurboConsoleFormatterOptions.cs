using Microsoft.Extensions.Logging.Console;

namespace Turbo.Logging;

internal class TurboConsoleFormatterOptions : ConsoleFormatterOptions
{
    public const string SECTION_NAME = "Logging:TurboConsole";

    /// <summary>Render timestamps in UTC (default: false = local).</summary>
    public new bool UseUtcTimestamp { get; set; } = false;

    /// <summary>Write a single line for message (default: true). Exceptions still render multi-line.</summary>
    public bool SingleLine { get; set; } = true;

    /// <summary>Include the category (default: true).</summary>
    public bool IncludeCategory { get; set; } = true;

    /// <summary>
    /// Trim category (namespace) to the last N segments.
    /// e.g., 1 => "RevisionManager", 2 => "Revisions.RevisionManager". 0 disables trimming.
    /// </summary>
    public int TrimCategoryDepth { get; set; } = 1;

    /// <summary>Enable ANSI color output (default: true).</summary>
    public bool UseAnsiColor { get; set; } = true;

    /// <summary>Include scopes if configured (default inherited from ConsoleFormatterOptions.IncludeScopes).</summary>
    public new bool IncludeScopes { get; set; } = true;
}
