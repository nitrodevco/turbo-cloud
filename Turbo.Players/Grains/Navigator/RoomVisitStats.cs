using System;

namespace Turbo.Players.Grains.Navigator;

/// <summary>How often a player has entered a room, and when last.</summary>
internal readonly record struct RoomVisitStats(int Visits, DateTime LastVisitUtc);
