using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Navigator;

public record CategoriesWithVisitorCountMessageComposer : IComposer
{
    public object? Data { get; init; }
}
