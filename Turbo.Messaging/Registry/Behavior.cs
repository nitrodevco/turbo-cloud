using System;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Messaging.Abstractions.Registry;

namespace Turbo.Messaging.Registry;

public sealed record Behavior(
    Func<IServiceProvider, object, MessageContext, Func<Task>, CancellationToken, Task> Invoke,
    int Order,
    string[] Tags
);
