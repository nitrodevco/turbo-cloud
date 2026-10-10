using System;
using System.Collections.Generic;
using Orleans;
using Turbo.Primitives.Gamedata.Enums;

namespace Turbo.Primitives.Gamedata.Snapshots;

/// <summary>
/// An asset job running, or the last one: what it is, the step it is on and how far through that
/// step, and the last lines of its log.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record AssetJobSnapshot
{
    [Id(0)]
    public required Guid Id { get; init; }

    [Id(1)]
    public required AssetJobKind Kind { get; init; }

    /// <summary>What it is in words: "Sync from Habbo PRODUCTION-202410...", "Publish to Live".</summary>
    [Id(2)]
    public required string Title { get; init; }

    [Id(3)]
    public required AssetJobStatus Status { get; init; }

    /// <summary>The step it is on, in words: "Listing", "Furniture", "Uploading".</summary>
    [Id(4)]
    public required string Phase { get; init; }

    /// <summary>Items in this step; zero while it is not known yet.</summary>
    [Id(5)]
    public required int Total { get; init; }

    /// <summary>Items of this step finished, failed ones included.</summary>
    [Id(6)]
    public required int Done { get; init; }

    /// <summary>Items of the whole job that failed.</summary>
    [Id(7)]
    public required int Failed { get; init; }

    /// <summary>Its log, oldest first; the oldest lines are let go past the configured limit.</summary>
    [Id(8)]
    public required IReadOnlyList<string> Log { get; init; }

    /// <summary>Why it failed.</summary>
    [Id(9)]
    public string? Error { get; init; }

    /// <summary>What it did, in a line, once done: "312 converted, 4 failed".</summary>
    [Id(10)]
    public string? Result { get; init; }

    [Id(11)]
    public required int PlayerId { get; init; }

    [Id(12)]
    public required DateTime StartedAt { get; init; }

    [Id(13)]
    public DateTime? FinishedAt { get; init; }
}
