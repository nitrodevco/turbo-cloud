using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Turbo.Primitives.Campaigns;

/// <summary>
/// Which door is which. Days count from 0, as the client does (the heading shows day index + 1,
/// <c>CalendarView.setSelectedIndex</c>). How the client draws a door from what it is sent is
/// Habbo's (<c>CalendarItem.resolveDayState</c>): opened, else later than today is future, else
/// missed when the server says so, else open to take. Which earlier doors the server calls missed
/// is not in the client: here a door can be opened on its own day only, so every earlier one not
/// opened is missed ("You're late! This gift can't be opened anymore.") - inferred.
/// </summary>
public static class CampaignCalendarRules
{
    /// <summary>
    /// Today's door: whole days since the start, -1 before it. Past the last day the calendar
    /// is over and this is at least <see cref="CampaignCalendarDefinition.Days"/>.
    /// </summary>
    public static int CurrentDay(CampaignCalendarDefinition campaign, DateTime utcNow) =>
        utcNow < campaign.StartsAt ? -1 : (int)Math.Floor((utcNow - campaign.StartsAt).TotalDays);

    /// <summary>On while it is enabled and today is one of its days.</summary>
    public static bool IsLive(CampaignCalendarDefinition campaign, DateTime utcNow)
    {
        var day = CurrentDay(campaign, utcNow);

        return campaign.Enabled && campaign.Days > 0 && day >= 0 && day < campaign.Days;
    }

    /// <summary>The doors before today a player did not open.</summary>
    public static ImmutableArray<int> MissedDays(int currentDay, IReadOnlySet<int> openedDays) =>
        [.. Enumerable.Range(0, Math.Max(0, currentDay)).Where(day => !openedDays.Contains(day))];

    /// <summary><c>CalendarItem.resolveDayState</c>, with the server's missed days.</summary>
    public static CampaignCalendarDoorState StateOf(
        int day,
        int currentDay,
        IReadOnlySet<int> openedDays
    )
    {
        if (openedDays.Contains(day))
            return CampaignCalendarDoorState.Unlocked;

        if (day > currentDay)
            return CampaignCalendarDoorState.Future;

        return day < currentDay
            ? CampaignCalendarDoorState.Expired
            : CampaignCalendarDoorState.Available;
    }
}
