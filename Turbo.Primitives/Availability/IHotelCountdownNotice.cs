using Turbo.Primitives.Messages.Outgoing.Turbo;

namespace Turbo.Primitives.Availability;

/// <summary>
/// The notice players are shown while a restart, a shutdown or a maintenance counts down, when the
/// hotel turns it on for that kind (<c>Turbo:Operations:RestartNotice</c> and the others): the
/// client's countdown notice under the purse, sent to clients that speak <c>notices.live</c>. Habbo's
/// own closing alerts are sent as they always are, beside it.
/// </summary>
public interface IHotelCountdownNotice
{
    /// <summary>The notice up now, for a client that has just asked for live notices; null when none.</summary>
    TurboNoticeMessage? Current { get; }

    /// <summary>Whether a countdown of this kind puts a notice up.</summary>
    bool IsEnabled(HotelCountdownKind kind);

    /// <summary>Where a notice of this kind links to when its countdown names nowhere of its own.</summary>
    string DefaultLink(HotelCountdownKind kind);
}
