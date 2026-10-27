using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace Turbo.Primitives.Logging;

/// <summary>
/// The server's latest log entries, kept in memory as the console is written (the last
/// <see cref="Capacity"/>, by the same <c>Logging</c> filters), for a developer's tools to show what
/// just happened without reading the console: the MCP plugin's <c>server_log</c>. A plugin's own
/// entries have its name and a colon before their category (<c>Skills:Skills.SkillBook</c>).
/// </summary>
public interface IServerLog
{
    /// <summary>How many entries are kept; the oldest goes as a new one comes.</summary>
    public int Capacity { get; }

    /// <summary>
    /// The newest <paramref name="take"/> entries of at least <paramref name="minimum"/>, newest
    /// first; with <paramref name="contains"/>, only those whose category, message or exception
    /// holds it (in any case).
    /// </summary>
    public IReadOnlyList<ServerLogEntry> Recent(
        int take,
        LogLevel minimum = LogLevel.Information,
        string? contains = null
    );

    /// <summary>The newest entry of level <see cref="LogLevel.Error"/> or above still kept, or null.</summary>
    public ServerLogEntry? LastError { get; }
}

/// <summary>
/// One log entry: <see cref="Sequence"/> counts every entry since the server started, so a gap
/// between two reads shows how many went by. <see cref="Exception"/> is the exception with its
/// stack trace (a plugin's frames name its file and line), null when none was logged.
/// </summary>
public sealed record ServerLogEntry(
    long Sequence,
    DateTime AtUtc,
    LogLevel Level,
    string Category,
    string Message,
    string? Exception
);
