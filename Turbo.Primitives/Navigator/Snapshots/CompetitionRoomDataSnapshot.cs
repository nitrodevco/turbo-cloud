using Orleans;

namespace Turbo.Primitives.Navigator.Snapshots;

[GenerateSerializer, Immutable]
public sealed record CompetitionRoomDataSnapshot
{
    [Id(0)]
    public required int GoalId;

    [Id(1)]
    public required int PageIndex;

    [Id(2)]
    public required int PageCount;
}
