namespace Turbo.Primitives.Campaigns;

/// <summary>A door as the client draws it: <c>CalendarItem.STATE_*</c>.</summary>
public enum CampaignCalendarDoorState
{
    /// <summary>Opened: "You have already opened this gift!"</summary>
    Unlocked = 1,

    /// <summary>Can be opened now: "You've got a gift! Click on the package to open it."</summary>
    Available = 2,

    /// <summary>Missed: "You're late! This gift can't be opened anymore."</summary>
    Expired = 3,

    /// <summary>Not yet: "Patience! This gift can't be opened yet."</summary>
    Future = 4,
}
