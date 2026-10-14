namespace Turbo.Primitives.Catalog.TargetedOffers;

/// <summary>
/// The tracking states the client sends with <c>SetTargetedOfferState</c> and reads back in the
/// offer (<c>OfferController</c>: <c>maximizeOffer</c> sends 1, <c>minimizeOffer</c> 4; an offer
/// arriving in state 4 docks in the toolbar, any other opens its dialog).
/// </summary>
public static class TargetedOfferTrackingStates
{
    /// <summary>Never shown to the player: the offer opens its dialog.</summary>
    public const int NEW = 0;

    public const int MAXIMIZED = 1;

    public const int MINIMIZED = 4;
}
