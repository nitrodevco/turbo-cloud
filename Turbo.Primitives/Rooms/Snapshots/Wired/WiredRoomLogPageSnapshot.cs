using System.Collections.Immutable;
using Orleans;

namespace Turbo.Primitives.Rooms.Snapshots.Wired;

/// <summary>
/// One page of the room's wired log ("View full logs", AS3 <c>WiredLogPage</c>): how many lines
/// match the filters, the page and its size, the lines, and the filters it was made with
/// (-1 / empty for none).
/// </summary>
[GenerateSerializer, Immutable]
public sealed record WiredRoomLogPageSnapshot
{
    [Id(0)]
    public required int TotalEntries { get; init; }

    [Id(1)]
    public required int Page { get; init; }

    [Id(2)]
    public required int PageSize { get; init; }

    [Id(3)]
    public required ImmutableArray<WiredRoomLogEntrySnapshot> Entries { get; init; }

    [Id(4)]
    public required int LogLevelFilter { get; init; }

    [Id(5)]
    public required int LogSourceFilter { get; init; }

    [Id(6)]
    public required string Query { get; init; }
}

/// <summary>One line of the room's wired log (AS3 <c>WiredLogEntry</c>).</summary>
[GenerateSerializer, Immutable]
public sealed record WiredRoomLogEntrySnapshot
{
    [Id(0)]
    public required long Id { get; init; }

    /// <summary><c>wiredmenu.logs_overview.log_level.&lt;n&gt;</c>: 0 INFO, 1 WARN, 2 ERROR, 3 DEBUG.</summary>
    [Id(1)]
    public required int Level { get; init; }

    /// <summary><c>wiredmenu.logs_overview.log_source.&lt;n&gt;</c>: 0 System, 1 Wired.</summary>
    [Id(2)]
    public required int Source { get; init; }

    [Id(3)]
    public required string Message { get; init; }

    [Id(4)]
    public required long TimestampMs { get; init; }

    [Id(5)]
    public required string TimestampText { get; init; }
}
