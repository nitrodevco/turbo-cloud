using System;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Events.Abstractions.Registry;

namespace Turbo.Events.Registry;

public sealed record Behavior(
    Func<IServiceProvider, object, EventContext, Func<Task>, CancellationToken, Task> Invoke,
    int Order,
    string[] Tags
);
