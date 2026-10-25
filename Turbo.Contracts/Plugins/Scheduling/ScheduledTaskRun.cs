using System;

namespace Turbo.Contracts.Plugins.Scheduling;

/// <summary>What one run of an <see cref="IScheduledTask"/> is.</summary>
/// <param name="Key">The task's key, <c>&lt;plugin key&gt;.&lt;name&gt;</c>, as the panel lists it.</param>
/// <param name="ScheduledFor">
/// The time the run was due (UTC). Later than due when the server was down or busy; the time staff
/// asked for it on a run now.
/// </param>
/// <param name="Manual">Whether staff ran it from the panel rather than its schedule.</param>
public sealed record ScheduledTaskRun(string Key, DateTimeOffset ScheduledFor, bool Manual);
