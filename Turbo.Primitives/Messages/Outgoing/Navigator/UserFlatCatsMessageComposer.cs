using System.Collections.Generic;
using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Navigator;

public sealed record UserFlatCatsMessageComposer : IComposer
{
    public List<object>? Nodes { get; init; }
}
