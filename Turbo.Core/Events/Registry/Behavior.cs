using System;
using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Core.Events.Registry;

public sealed record Behavior(
    Func<IServiceProvider, object, EventContext, Func<Task>, CancellationToken, Task> Invoke,
    int Order,
    string[] Tags
);
