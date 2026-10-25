# Campaign calendar

Habbo's gift calendar (advent-style): one door a day, each with a gift. The client is
`com/sulake/habbo/campaign` in the Flash client (`HabboCampaigns`, `CalendarView`, `CalendarItem`,
read from WIN63-202610201032); the window is `habbo-toolbar-com/campaign_calendar_xml`.

## How it runs

- The client never asks for the calendar: it keeps the last `CampaignCalendarData` it was sent and
  opens the window on the `openView/calendar` link (`HabboCampaigns.linkReceived`). The server
  sends it at login when a calendar is live (the latest started one when several are).
- `CampaignCalendarData` = campaign name, image, today's day index, number of days, the days
  opened, the days missed. Days count from 0; the window's heading shows `Day <index + 1>`.
- A door is drawn (`CalendarItem.resolveDayState`) as: opened; else **future** when its day is
  after today; else **missed** when the server lists it; else **available**. Here a door can be
  opened on its own day only, so every earlier one not opened is missed (inferred: Habbo's
  client does not say which earlier doors its server still lets players open).
- A click on the selected door sends `OpenCampaignCalendarDoor(name, day)`. The server opens it
  once per player, gives the gift and answers `CampaignCalendarDoorOpened(true, product code,
  custom image, furni class name)`; anything else answers `false`, which the client ignores.
- The client shows the door opened only when its product data knows the product code
  (`HabboCampaigns.showProductNotification`); it then draws the custom image (under
  `image.library.url`) or else the furni's icon.
- **Staff force open**: the client shows the `FORCE OPEN` button at security level 5
  (`isAnyRoomController`) and sends `OpenCampaignCalendarDoorAsStaff(name, day)`. The server lets
  it through only with the node `campaign.calendar.force_open`; it opens any day of the live
  calendar that is not opened yet, missed and future ones too.
- Texts: `campaign.calendar.<name>.title`, `campaign.calendar.heading.day`,
  `campaign.calendar.heading.product.received`, `campaign.calendar.info.*` (hotel texts).

## Data

`campaign_calendars` (name, image, `starts_at` in UTC, days, enabled), `campaign_calendar_rewards`
(one per day: product code, custom image, furni class name, badge, credits, a currency and amount,
an effect) and `player_campaign_calendar_doors` (who opened which door, and whether by the staff
force open). Day `n`'s door opens at `starts_at + n days`. A gift's credits and currency are paid
under the reference `calendar:<campaign id>:<day>` and its furni given once under it, so a retry
never pays twice; a gift that cannot be given leaves the door closed. Movements use the cause
`reward.campaign_calendar`.

## Admin API

`/api/campaign-calendars` (node `admin.campaigns.manage`): `GET /`, `GET /{id}`, `POST /`,
`PUT /{id}`, `DELETE /{id}`. A calendar is refused when a day lies outside it, has two gifts, names
no product (or furni) for the client to show, names a furni or currency the hotel lacks, or pays a
negative amount.

An example (nothing is seeded):

```json
{
  "name": "example_advent",
  "image": "",
  "startsAt": "2026-12-01T00:00:00Z",
  "days": 24,
  "enabled": true,
  "rewards": [
    { "dayIndex": 0, "furnitureClassName": "throne" },
    { "dayIndex": 1, "productCode": "credits_100", "credits": 100 },
    { "dayIndex": 2, "productCode": "throne", "badgeCode": "ADV01" }
  ]
}
```

## Extending

`ICampaignCalendarService` is the whole feature; a plugin that registers its own replaces it (other
rules for missed doors, gifts from elsewhere).
