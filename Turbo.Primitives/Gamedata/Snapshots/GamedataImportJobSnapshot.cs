using System;
using Orleans;
using Turbo.Primitives.Gamedata.Enums;

namespace Turbo.Primitives.Gamedata.Snapshots;

/// <summary>A Habbo release being taken in, or taken in last.</summary>
[GenerateSerializer, Immutable]
public sealed record GamedataImportJobSnapshot
{
    /// <summary>The release taken in, or the version of Habbo's texts.</summary>
    [Id(0)]
    public required int ReleaseId { get; init; }

    /// <summary>What is taken in (<see cref="GamedataFiles"/>): furniture, or texts.</summary>
    [Id(10)]
    public required string File { get; init; }

    [Id(1)]
    public required string Revision { get; init; }

    [Id(2)]
    public required GamedataImportPhase Phase { get; init; }

    /// <summary>Furniture asset files to read.</summary>
    [Id(3)]
    public required int FilesTotal { get; init; }

    [Id(4)]
    public required int FilesDone { get; init; }

    /// <summary>Files that could not be fetched or read; their furniture keeps its states.</summary>
    [Id(5)]
    public required int FilesFailed { get; init; }

    /// <summary>What it changed, once done; null when the hotel already had everything.</summary>
    [Id(6)]
    public GamedataChangeSetSnapshot? ChangeSet { get; init; }

    /// <summary>Why it failed.</summary>
    [Id(7)]
    public string? Error { get; init; }

    [Id(8)]
    public required DateTime StartedAt { get; init; }

    [Id(9)]
    public DateTime? FinishedAt { get; init; }
}
