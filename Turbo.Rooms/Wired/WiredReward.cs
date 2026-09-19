using Turbo.Primitives.Rooms.Enums.Wired;

namespace Turbo.Rooms.Wired;

/// <summary>One prize of a "give reward" box.</summary>
public sealed record WiredReward(WiredRewardType Type, string Code, int Probability);
