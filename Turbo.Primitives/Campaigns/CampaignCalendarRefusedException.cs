using System;

namespace Turbo.Primitives.Campaigns;

/// <summary>
/// A calendar <see cref="ICampaignCalendarService.SaveAsync"/> refuses, with what it is about: a
/// field of the calendar (<c>name</c>, <c>image</c>, <c>days</c>) or of one day's gift
/// (<see cref="DayIndex"/>, and <c>dayIndex</c>, <c>productCode</c>, <c>furnitureClassName</c>,
/// <c>credits</c>, <c>currencyAmount</c> or <c>currencyTypeId</c>), so the panel can show it there.
/// </summary>
public sealed class CampaignCalendarRefusedException(
    string message,
    string field,
    int? dayIndex = null
) : ArgumentException(message)
{
    public string Field { get; } = field;

    public int? DayIndex { get; } = dayIndex;
}
