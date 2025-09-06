using System;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Messaging.Abstractions.Registry;

namespace Turbo.Messaging.Registry;

public sealed record Handler(
    Type ServiceType,
    Func<object, object, MessageContext, CancellationToken, Task> Invoke,
    int Order,
    string[] Tags
);
