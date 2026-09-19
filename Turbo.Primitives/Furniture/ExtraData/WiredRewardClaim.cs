using System.Collections.Generic;

namespace Turbo.Primitives.Furniture.ExtraData;

/// <summary>What one player has claimed from a "give reward" box; stored on the box.</summary>
public sealed class WiredRewardClaim
{
    public int Count { get; set; }
    public long LastClaimUnix { get; set; }
    public HashSet<string> ReceivedCodes { get; set; } = [];
}
