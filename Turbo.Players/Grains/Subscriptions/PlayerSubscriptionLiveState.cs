using System.Collections.Generic;
using Turbo.Database.Entities.Players;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Enums;

namespace Turbo.Players.Grains.Subscriptions;

internal sealed class PlayerSubscriptionLiveState
{
    public required PlayerId PlayerId { get; init; }

    /// <summary>The player's rows, one per type. A type with no row has never been bought.</summary>
    public Dictionary<SubscriptionType, PlayerSubscriptionEntity> SubscriptionsByType { get; } = [];
}
