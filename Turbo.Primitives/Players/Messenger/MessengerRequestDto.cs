using Orleans;

namespace Turbo.Primitives.Players.Messenger;

[GenerateSerializer]
public sealed record MessengerRequestDto
{
    [Id(0)]
    public required int RequestId { get; init; }

    [Id(1)]
    public required PlayerId RequesterPlayerId { get; init; }

    [Id(2)]
    public required string RequesterName { get; init; }

    [Id(3)]
    public required string RequesterFigure { get; init; }
}
