namespace Turbo.Primitives.Players.Enums;

/// <summary>
/// A subscription a player can hold. One row of <c>player_subscriptions</c> per player and type,
/// owned by <c>IPlayerSubscriptionGrain</c>.
/// </summary>
public enum SubscriptionType
{
    HabboClub = 0,
    BuildersClub = 1,
}
