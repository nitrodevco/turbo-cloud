using Turbo.Primitives.Rooms.Events;
using Turbo.Primitives.Rooms.Wired;

namespace Turbo.Rooms.Wired;

/// <summary>
/// What the context variables of the wired execution running now read
/// (sirjonasxx, variables-info #9 and #13): what its selectors picked, what a signal forwarded,
/// the event that started it and its trigger.
/// </summary>
public sealed record WiredRunningExecution(
    IWiredSelectionSet SelectorPool,
    IWiredSelectionSet Signal,
    RoomEvent? Event,
    IWiredTrigger? Trigger
);
