using System;
using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Core.Events.Registry;

public sealed record Handler(
    Type ServiceType,
    Func<object, object, EventContext, CancellationToken, Task> Invoke,
    int Order,
    string[] Tags
);
