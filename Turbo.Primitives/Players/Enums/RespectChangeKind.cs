namespace Turbo.Primitives.Players.Enums;

/// <summary>What a respect did to one player's counters.</summary>
public enum RespectChangeKind : byte
{
    /// <summary>One of today's respects spent on another player.</summary>
    Spent = 0,

    /// <summary>One of today's pet respects spent on a pet (a scratch).</summary>
    PetSpent = 1,

    /// <summary>A respect received from another player.</summary>
    Received = 2,
}
