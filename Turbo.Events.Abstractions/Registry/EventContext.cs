using System;

namespace Turbo.Events.Abstractions.Registry;

public class EventContext
{
    public required IServiceProvider Services { get; init; }
}
