using System;
using System.Collections.Generic;
using Turbo.Primitives.Hotel.Enums;

namespace Turbo.Admin.Api.Contracts;

/// <summary>A community goal as staff write it; times are UTC.</summary>
public sealed record CommunityGoalRequest(
    string? Code,
    CommunityGoalMode? Mode,
    DateTime? StartsAt,
    DateTime? EndsAt,
    List<int>? LevelScores,
    List<int>? RewardRanks,
    int? SideOnePageId,
    int? SideTwoPageId
);
