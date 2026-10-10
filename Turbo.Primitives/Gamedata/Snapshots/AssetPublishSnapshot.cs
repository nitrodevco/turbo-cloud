using System;

namespace Turbo.Primitives.Gamedata.Snapshots;

/// <summary>A publish, as its target's history keeps it: who started it, what it sent, skipped and removed, and how it ended.</summary>
public sealed record AssetPublishSnapshot
{
    public required int Id { get; init; }

    public required int TargetId { get; init; }

    public required int PlayerId { get; init; }

    /// <summary>Only counted what would be sent; nothing was.</summary>
    public required bool DryRun { get; init; }

    public required int Uploaded { get; init; }

    /// <summary>Bundles the target already held as they are.</summary>
    public required int Skipped { get; init; }

    public required int Deleted { get; init; }

    /// <summary>Bytes sent.</summary>
    public required long Bytes { get; init; }

    /// <summary>Why it failed or stopped, or how many files could not be sent.</summary>
    public string? Error { get; init; }

    public required DateTime StartedAt { get; init; }

    /// <summary>Null while it runs, or when the server stopped under it.</summary>
    public DateTime? FinishedAt { get; init; }
}
