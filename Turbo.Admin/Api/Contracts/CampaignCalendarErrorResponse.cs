namespace Turbo.Admin.Api.Contracts;

/// <summary>
/// A calendar refused: why, and the field it is about (and the day of a gift's), for the panel to
/// show it there.
/// </summary>
public sealed record CampaignCalendarErrorResponse(string Message, string Field, int? DayIndex);
