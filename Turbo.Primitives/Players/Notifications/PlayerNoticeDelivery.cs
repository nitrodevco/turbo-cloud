namespace Turbo.Primitives.Players.Notifications;

/// <summary>Sent means submitted to the active session, not acknowledged or read by the player.</summary>
public enum PlayerNoticeDelivery
{
    Sent,
    Offline,
    Failed,
}
