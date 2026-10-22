using System;
using System.Collections.Generic;
using Turbo.Primitives.Players;

namespace Turbo.Players.Grains.Onboarding;

internal sealed class PlayerNuxGiftLiveState
{
    public required PlayerId PlayerId { get; init; }

    /// <summary>When the player signed up; null for a player that does not exist.</summary>
    public DateTime? SignedUpAt { get; set; }

    /// <summary>The steps the player has picked from, by claim id (<c>nux:day:step</c>).</summary>
    public HashSet<string> Taken { get; } = [];
}
