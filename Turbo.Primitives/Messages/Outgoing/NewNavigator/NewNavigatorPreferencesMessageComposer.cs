using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.NewNavigator;

[GenerateSerializer, Immutable]
public sealed record NewNavigatorPreferencesMessageComposer : IComposer
{
    [Id(0)]
    public required int WindowX { get; init; }

    [Id(1)]
    public required int WindowY { get; init; }

    [Id(2)]
    public required int WindowWidth { get; init; }

    [Id(3)]
    public required int WindowHeight { get; init; }

    [Id(4)]
    public required bool LeftPaneHidden { get; init; }

    [Id(5)]
    public required int ResultsMode { get; init; }
}
