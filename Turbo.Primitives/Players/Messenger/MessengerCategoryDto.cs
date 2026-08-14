using Orleans;

namespace Turbo.Primitives.Players.Messenger;

[GenerateSerializer]
public sealed record MessengerCategoryDto
{
    [Id(0)]
    public required int CategoryId { get; init; }

    [Id(1)]
    public required string Name { get; init; }
}
