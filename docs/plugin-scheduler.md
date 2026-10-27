# Scheduled tasks

- [Introduction](#introduction)
- [A weekly job](#a-weekly-job)
- [Schedules](#schedules)
- [A schedule staff can move](#a-schedule-staff-can-move)
- [Testing a task](#testing-a-task)
- [One run per server](#one-run-per-server)
- [The admin API](#the-admin-api)
- [Not in this](#not-in-this)

## Introduction

A plugin that has to do something on a clock (reprice a shop every Monday, hand out a daily prize,
expire offers every five minutes) declares a scheduled task. There is no loop to write: the server
finds the task as the plugin loads, runs it on its schedule, and takes it away when the plugin
unloads.

What the server promises:

- **One run per cluster.** However many servers the hotel runs, each run happens on one of them.
  The schedule is kept by one Orleans grain for the whole cluster; every server tells it which tasks
  it has loaded, and it asks one of them for each run. A task about what one server holds in its
  own memory says `PerServer = true` instead (see [One run per server](#one-run-per-server)).
- **Never two runs at once.** A run that falls due while the one before is still going is skipped
  and counted (the panel shows how many). There is no queue of late runs.
- **Missed runs are skipped, unless you ask.** When no server was up at the time a run was due, it
  is not run later, and the next run is the next one due from then. A task with `RunMissed = true`
  runs once as the first server with it starts, however many runs it missed.
- **The schedule survives restarts.** Each task's last run is kept in the database
  (`scheduled_task_runs`, one row per task, updated in place), so a weekly job counts its week from
  the last time it ran, not from the last restart. A task due again within a minute writes its
  routine runs at most once a minute (a restart counts it from then anyway); a failure, a cancel,
  a run now and any task due less often are written as they end.
- **Quiet in the log.** A routine run is logged at `Trace`, below the shipped `"Turbo": "Information"` (and the bootstrap's development `Debug`),
  so a task every few seconds does not fill the log. Its first run on a server, a run now and a
  cancelled run are `Information`, a failure is an `Error` with its exception, and a skipped run
  is a `Warning`.
- **Unload cancels.** A plugin that unloads (a reload, a disable, the server stopping) has its
  running task's `CancellationToken` cancelled, and the unload waits for the run to end (up to 10
  seconds) before the plugin's own `StopAsync` and before its services are disposed.
- **Listed in the panel.** The Plugins page's Schedule tab lists every task with its schedule in
  words, its last run (when, how long, how it ended, the error), its next run, and a **Run now**
  button for staff with `admin.plugins.manage`. A run now does not move the schedule.

## A weekly job

```csharp
using Turbo.Contracts.Plugins.Scheduling;

[Schedule("0 18 * * MON")]          // Mondays 18:00, server time
public sealed class WeeklyRotation(RareShop shop, ILogger<WeeklyRotation> logger) : IScheduledTask
{
    public async Task RunAsync(ScheduledTaskRun run, CancellationToken ct)
    {
        var moved = await shop.RotateAsync(ct);

        logger.LogInformation("Rotated {Count} rares (due {Due:u})", moved, run.ScheduledFor);
    }
}
```

That is all: a public class implementing `IScheduledTask` anywhere in the plugin's assembly. It is
built once with the plugin's services (and the hotel's, as commands are). Its key is
`<plugin key>.<class name>` (`rares.WeeklyRotation`); `Name = "..."` on the attribute changes the
second half. The template's `server/SkillsWeeklyReset.cs` is a working example, and
`npm run make schedule weekly-rotation --cron "0 18 * * MON"` (or `--every 00:05:00`) in a plugin
made from it writes `server/Tasks/WeeklyRotation.cs`.

`run.ScheduledFor` is when the run was due; `run.Manual` is true for a run now. A run that throws is
logged and shown in the panel as failed; the next one runs on schedule.

## Schedules

| Form | Means |
| --- | --- |
| `[Schedule("0 18 * * MON")]` | Cron: minute hour day-of-month month day-of-week. `*`, lists (`1,15`), ranges (`1-5`), steps (`*/10`), names (`JAN`-`DEC`, `SUN`-`SAT`), `0` or `7` for Sunday. A day matches when either day field does if both are given, as cron reads it. |
| `[Schedule("@daily")]` | `@hourly`, `@daily`, `@weekly` (Sundays 00:00), `@monthly`, `@yearly`. |
| `[Schedule("30 1 * * *", TimeZone = "Europe/London")]` | In that time zone; otherwise the server's own. |
| `[Every("00:05:00")]` | Every five minutes, counted from the last run that was due. At least a second. |
| none | Runs only when staff press Run now. |

Clock changes: a time the clocks skip as they go forward runs once as they do (01:30 on the night
the clocks go from 01:00 to 02:00 runs at 02:00). A time that comes twice as they go back runs once,
the first time, unless the hour field is `*` (then a job every 30 minutes keeps its pace through the
repeated hour).

## A schedule staff can move

Drop the attribute and return the schedule from a setting: `Schedule` is read each time the next
run is worked out, so a save on the panel's Settings page moves the task within seconds.

```csharp
public sealed class RareSettings
{
    public const string SECTION_NAME = "Rares";

    /// <summary>When the shop rotates (cron, or an interval such as 1.00:00:00).</summary>
    public string RotationSchedule { get; init; } = "0 18 * * MON";
}

public sealed class WeeklyRotation(RareShop shop, IOptionsMonitor<RareSettings> settings)
    : IScheduledTask
{
    public string? Schedule => settings.CurrentValue.RotationSchedule;

    public Task RunAsync(ScheduledTaskRun run, CancellationToken ct) => shop.RotateAsync(ct);
}
```

A setting may name a time zone after the five fields (`0 18 * * MON Europe/London`). A schedule
that can't be read is shown in the panel with the reason, and the task runs only on Run now until
it is fixed.

## Testing a task

A task takes what it needs by constructor like any plugin class, the hotel API included
(`IHotel`, `docs/hotel-api.md`), so its test builds it over a `FakeHotel` from `Turbo.Testing`.
Two helpers there read the fake hotel's clock (`hotel.Clock`, which stands still until the test
moves it):

```csharp
var hotel = new FakeHotel(new DateTimeOffset(2026, 10, 24, 12, 0, 0, TimeSpan.Zero));
var job = new WeeklyRotation(shop, hotel);

Assert.Equal(mondayEvening, hotel.NextRunOf(job));   // by its Schedule, else its attribute
hotel.Clock.Set(mondayEvening);
await hotel.RunAsync(job);                            // one run, due now
hotel.Notify.AssertAlerted(alice, WeeklyRotation.ROTATED);
```

`NextRunOf` reads the schedule as the server does (`ScheduledTasks.ScheduleOf`), so a schedule
from a setting is tested with the setting's value. The template's
`tests/Skills.Tests/SkillsWeeklyResetTests.cs` is a working example. `TaskSchedule.Parse(text)
.Next(after)` and `.Describe()` (in `Turbo.Primitives`) check a schedule on its own.

## One run per server

By default a task runs once per tick for the whole cluster: right for anything that changes the
database, pays players or sends hotel-wide notices, as two servers doing it would do it twice.
A task that works on what one server holds in its own memory (the players online there, a cache it
keeps, pending checks it remembers) would then tick on one server only. Such a task says so:

```csharp
[Every("00:00:30", PerServer = true)]
public sealed class AfkCheck(AfkWatch watch) : IScheduledTask
{
    public Task RunAsync(ScheduledTaskRun run, CancellationToken ct) => watch.SweepAsync(ct);
}
```

Every server that has the plugin loaded runs it on its own clock, whether or not it can reach the
scheduler grain, never two runs at once on the same server. The panel lists it once with how many
servers have it, its last run is the latest any of them reported, and **Run now** runs it once on
every one of them. Its runs count from the server's start, so `RunMissed` does not apply.

## The admin API

Authenticated as [admin-panel.md](admin-panel.md#calling-the-admin-api) says (`Authorization: Bearer
<session token>`); the panel's Plugins page makes the same calls.

| Endpoint | Needs | Answers |
| --- | --- | --- |
| `GET /api/plugins/schedule` | `admin.plugins.view` | `{ tasks: [{ key, plugin, schedule, description, scheduleError, nextRunAt, lastRunAt, lastDurationMs, lastOutcome, lastError, lastManual, runningSince, skipped, servers, perServer }], canManage }` (times UTC) |
| `POST /api/plugins/schedule/{key}/run` | `admin.plugins.manage` | 200 started, 409 already running, 404 no such task, 503 no server could run it. Written to the command log. |

## Not in this

- Core's own periodic jobs (the maintenance countdown, online rewards) keep their loops for now.
- Runs are whole minutes for cron and whole seconds for intervals; the scheduler looks once a
  second, so a run starts up to a second after it is due.
