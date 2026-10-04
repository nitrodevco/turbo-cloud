using System;
using System.Collections.Generic;

namespace Turbo.Admin.Api.Contracts;

/// <summary>How the hotel is running: what <c>:status</c> says, and the busiest rooms.</summary>
public sealed record DashboardResponse(
    string Version,
    DateTime StartedAtUtc,
    int PlayersOnline,
    int RoomsLoaded,
    int SilosActive,
    int SilosTotal,
    long WorkingSetMb,
    long ManagedMb,
    string Availability,
    DateTime? AvailabilityAtUtc,
    IReadOnlyList<DashboardRoom> BusiestRooms
);
