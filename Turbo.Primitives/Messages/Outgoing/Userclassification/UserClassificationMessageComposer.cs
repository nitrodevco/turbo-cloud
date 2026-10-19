using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Moderation.Snapshots;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Userclassification;

/// <summary>
/// The players a <c>:uc</c> or <c>:anew</c> asked for; the client opens a user classification
/// window with them, even when there are none.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record UserClassificationMessageComposer : IComposer
{
    [Id(0)]
    public required ImmutableArray<UserClassificationSnapshot> Users { get; init; }
}
