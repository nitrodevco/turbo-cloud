using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;

namespace Turbo.Logging;

internal sealed class TurboConsoleFormatter(
    IOptionsMonitor<TurboConsoleFormatterOptions> optionsMonitor
) : ConsoleFormatter(FormatterName)
{
    public const string FormatterName = "turbo";

    private readonly IOptionsMonitor<TurboConsoleFormatterOptions> _optionsMonitor = optionsMonitor;

    // Level label & color mappings
    private static readonly Dictionary<LogLevel, string> s_levelLabels = new()
    {
        [LogLevel.Trace] = "TRC",
        [LogLevel.Debug] = "DBG",
        [LogLevel.Information] = "INF",
        [LogLevel.Warning] = "WRN",
        [LogLevel.Error] = "ERR",
        [LogLevel.Critical] = "CRT",
        [LogLevel.None] = "NON",
    };

    private static readonly Dictionary<LogLevel, (string fg, string bg)> s_levelColors = new()
    {
        [LogLevel.Trace] = ("\u001b[90m", ""), // Bright Black (Gray)
        [LogLevel.Debug] = ("\u001b[36m", ""), // Cyan
        [LogLevel.Information] = ("\u001b[37m", ""), // White
        [LogLevel.Warning] = ("\u001b[33m", ""), // Yellow
        [LogLevel.Error] = ("\u001b[31m", ""), // Red
        [LogLevel.Critical] = ("\u001b[97m", "\u001b[41m"), // White on Red background
        [LogLevel.None] = ("\u001b[37m", ""),
    };

    private const string RESET = "\u001b[0m";

    public override void Write<TState>(
        in LogEntry<TState> logEntry,
        IExternalScopeProvider? scopeProvider,
        TextWriter textWriter
    )
    {
        var options = _optionsMonitor.CurrentValue;

        var now = options.UseUtcTimestamp ? DateTimeOffset.UtcNow : DateTimeOffset.Now;
        var ts = now.ToString(options.TimestampFormat ?? "yyyy-MM-dd HH:mm:ss.fff");

        var level = logEntry.LogLevel;
        var levelLabel = s_levelLabels.TryGetValue(level, out var lbl)
            ? lbl
            : level.ToString().ToUpperInvariant();

        string? category = null;
        if (options.IncludeCategory && !string.IsNullOrEmpty(logEntry.Category))
            category = TrimCategory(logEntry.Category!, options.TrimCategoryDepth);

        var (fg, bg) = s_levelColors[level];

        // Build header (timestamp + level + category)
        var headerSb = new StringBuilder(128);
        if (options.UseAnsiColor)
        {
            headerSb.Append('[').Append(ts).Append("] ");
            if (bg.Length > 0)
                headerSb.Append(bg);
            headerSb.Append(fg).Append(levelLabel).Append(RESET);
            if (!string.IsNullOrEmpty(bg))
                headerSb.Append(RESET);
        }
        else
        {
            headerSb.Append('[').Append(ts).Append("] ").Append(levelLabel);
        }

        if (!string.IsNullOrEmpty(category))
            headerSb.Append(' ').Append('(').Append(category).Append(')');

        if (logEntry.EventId.Id != 0)
            headerSb.Append(" [").Append(logEntry.EventId.Id).Append(']');

        var header = headerSb.ToString();

        // ===== Align after the category =====
        // Assume max width for the header column
        const int HEADER_WIDTH = 58;
        var paddedHeader = header.PadRight(HEADER_WIDTH);

        var message = logEntry.Formatter?.Invoke(logEntry.State, logEntry.Exception);
        if (!string.IsNullOrEmpty(message))
        {
            if (options.SingleLine)
                message = message
                    .Replace(Environment.NewLine, " ")
                    .Replace('\n', ' ')
                    .Replace('\r', ' ');
        }

        var sb = new StringBuilder(256);
        sb.Append(paddedHeader);

        if (!string.IsNullOrEmpty(message))
            sb.Append(": ").Append(message);

        if (options.IncludeScopes && scopeProvider is not null)
        {
            scopeProvider.ForEachScope(
                (scope, s) =>
                {
                    s.Append(" => ").Append(scope);
                },
                sb
            );
        }

        // Write the line
        textWriter.WriteLine(sb.ToString());

        // Write the exception (on next line, aligned as well)
        if (logEntry.Exception is not null)
        {
            var exceptionHeader = "".PadLeft(HEADER_WIDTH);
            if (options.UseAnsiColor)
                textWriter.WriteLine(
                    $"{exceptionHeader}{s_levelColors[LogLevel.Error].fg}{logEntry.Exception}{RESET}"
                );
            else
                textWriter.WriteLine($"{exceptionHeader}{logEntry.Exception}");
        }
    }

    private static string TrimCategory(string category, int depth)
    {
        if (depth <= 0)
            return category;

        // Split by '.' and take last N segments
        var parts = category.Split('.');
        if (parts.Length <= depth)
            return category;

        var start = parts.Length - depth;
        return string.Join('.', parts, start, depth);
    }
}
