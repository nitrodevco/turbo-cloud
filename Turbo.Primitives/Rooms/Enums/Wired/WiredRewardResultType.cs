namespace Turbo.Primitives.Rooms.Enums.Wired;

/// <summary>Result codes of the reward result packet (client wired.reward.&lt;code&gt; texts).</summary>
public enum WiredRewardResultType
{
    RewardNotFound = 0,
    LimitReached = 1,
    RewardAlreadyReceived = 2,
    ProbabilityMissed = 3,
    RewardReceivedBadge = 4,
    RewardReceivedProduct = 5,
    RewardReceivedCredits = 6,
    RewardReceivedActivityPoints = 7,
    Ineligible = 8,
}
