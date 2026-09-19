using System.Collections.Generic;

namespace Turbo.Primitives.Furniture.ExtraData;

/// <summary>
/// What one player has claimed from a "give reward" box. The box keeps a map of player id to
/// claim under <see cref="SECTION"/>, so limits survive a room reload.
/// </summary>
public sealed class WiredRewardClaim
{
    public const string SECTION = "wired_rewards";

    public int Count { get; set; }
    public long LastClaimUnix { get; set; }
    public HashSet<string> ReceivedCodes { get; set; } = [];
}
