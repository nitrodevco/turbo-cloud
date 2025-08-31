using System;

namespace Turbo.Core.Events.Registry;

public class EventContext
{
    public required IServiceProvider Services { get; init; }
}
