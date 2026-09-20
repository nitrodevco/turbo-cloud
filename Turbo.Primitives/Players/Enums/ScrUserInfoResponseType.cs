namespace Turbo.Primitives.Players.Enums;

/// <summary>
/// Why the client is being told about its Habbo Club subscription. The client branches on this
/// in <c>HabboCatalog.onSubscriptionInfo</c> and <c>HabboInventory.setClubStatus</c>: it is the
/// difference between an answer to a question it asked and news it has to react to.
/// </summary>
public enum ScrUserInfoResponseType
{
    /// <summary>An answer to <c>ScrGetUserInfo</c>; the client only refreshes its purse.</summary>
    Normal = 0,

    /// <summary>The subscription changed, so the client resets and reloads the catalog.</summary>
    SubscriptionChanged = 2,

    /// <summary>The subscription runs out soon; the client shows it as expiring.</summary>
    Expiring = 3,

    /// <summary>The citizenship VIP period runs out soon. Nothing grants that yet.</summary>
    CitizenshipVipExpiring = 4,
}
