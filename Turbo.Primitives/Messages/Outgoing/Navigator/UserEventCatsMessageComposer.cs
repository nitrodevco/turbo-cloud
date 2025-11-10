using System.Collections.Generic;
using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Navigator;

public sealed record UserEventCatsMessageComposer : IComposer
{
    public List<object>? EventCategories { get; init; }
}
