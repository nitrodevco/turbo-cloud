using System;
using System.Linq;

namespace Turbo.Admin.Api.Contracts;

/// <summary>
/// A track in the panel's list: when players get it (<see cref="Live"/> = on and within its
/// dates now) and how much it holds.
/// </summary>
public sealed record RewardTrackListItem(
    string Id,
    string Theme,
    bool Enabled,
    DateTime? StartsAt,
    DateTime? EndsAt,
    bool Live,
    bool HasPremium,
    int Tasks,
    int Levels,
    int Prizes,
    int MaxPoints
);

/// <summary>The tracks in their new order; any not named keep their order after them.</summary>
public sealed record RewardTrackOrderRequest(string[]? TrackIds);

/// <summary>The furni a furni prize gives here: its definition id and class name.</summary>
public sealed record RewardTrackPrizeFurni(string PrizeId, int DefinitionId, string Name);

/// <summary>
/// Habbo's reward tracks to take in from a capture: what its client was sent at login
/// (<c>RewardTracks</c>), as the client reads it. <see cref="Only"/> takes in just those track
/// ids; empty takes in every one.
/// </summary>
public sealed record RewardTrackImportRequest(RewardTrackCapture? Capture, string[]? Only);

/// <summary>The decoded <c>RewardTracks</c> message: its tracks, in the order sent.</summary>
public sealed record RewardTrackCapture(RewardTrackCaptureTrack[]? Tracks);

/// <summary>A track as <c>RewardTracks</c> carries it (the player's progress is not read).</summary>
public sealed record RewardTrackCaptureTrack(
    string? Id,
    string? Theme,
    bool HasPremiumConfig,
    double TaskPointsBoost,
    int InstantPoints,
    int CostDiamonds,
    int CostCredits,
    RewardTrackCaptureTask[]? Tasks,
    RewardTrackCapturePrize[]? Prizes
);

public sealed record RewardTrackCaptureTask(
    string? Id,
    string? ActionType,
    string? Parameter,
    bool Premium,
    RewardTrackCaptureLevel[]? Levels
);

public sealed record RewardTrackCaptureLevel(int RequiredCount, int PointsReward, bool Premium);

/// <summary>A prize: <see cref="ProductItemTypeId"/> is AS3 ProductIconWidget's product item type.</summary>
public sealed record RewardTrackCapturePrize(
    string? Id,
    int RequiredPoints,
    short ProductItemTypeId,
    string? RewardTypeId,
    string? ExtraParams,
    int RewardAmount,
    bool Premium
);

/// <summary>
/// What taking a capture in does, or did, track by track; <see cref="Saved"/> once it is made.
/// </summary>
public sealed record RewardTrackImportPlan(RewardTrackImportChange[] Tracks, bool Saved)
{
    public bool Changes => Tracks.Any(x => x.Action != RewardTrackImportChange.UNCHANGED);
}

/// <summary>
/// One track of the capture: added, changed (its premium or theme, and the tasks and prizes added,
/// changed and removed) or unchanged. A changed track keeps the hotel's own choices: on or off,
/// its dates, and the furni definition and name a kept prize gives. <see cref="Warnings"/> lists
/// what this hotel cannot do of it: a task type the server does not count, a prize it cannot give.
/// </summary>
public sealed record RewardTrackImportChange(
    string Id,
    string Action,
    bool PremiumChanged,
    bool ThemeChanged,
    string[] TasksAdded,
    string[] TasksChanged,
    string[] TasksRemoved,
    string[] PrizesAdded,
    string[] PrizesChanged,
    string[] PrizesRemoved,
    string[] Warnings
)
{
    public const string ADDED = "added";
    public const string CHANGED = "changed";
    public const string UNCHANGED = "unchanged";
}
