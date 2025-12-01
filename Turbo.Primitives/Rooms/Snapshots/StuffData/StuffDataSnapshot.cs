using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Orleans;
using Turbo.Primitives.Furniture.StuffData;

namespace Turbo.Primitives.Rooms.Snapshots.StuffData;

[GenerateSerializer, Immutable]
public sealed record StuffDataSnapshot
{
    [Id(0)]
    public required int StuffBitmask { get; init; }

    [Id(1)]
    public int UniqueNumber { get; init; }

    [Id(2)]
    public int UniqueSeries { get; init; }

    [Id(3)]
    public LegacyStuffPayload? LegacyPayload { get; init; }

    [Id(4)]
    public MapStuffPayload? MapPayload { get; init; }

    [Id(5)]
    public StringStuffPayload? StringPayload { get; init; }

    [Id(6)]
    public VoteStuffPayload? VotePayload { get; init; }

    [Id(7)]
    public NumberStuffPayload? NumberPayload { get; init; }

    [Id(8)]
    public HighscoreStuffPayload? HighscorePayload { get; init; }

    public static StuffDataSnapshot FromStuffData(IStuffData data)
    {
        var snapshot = new StuffDataSnapshot
        {
            StuffBitmask = data.GetBitmask(),
            UniqueNumber = data.UniqueNumber,
            UniqueSeries = data.UniqueSeries,
        };

        switch (data)
        {
            case ILegacyStuffData legacy:
                snapshot = snapshot with
                {
                    LegacyPayload = new LegacyStuffPayload { Data = legacy.GetLegacyString() },
                };
                break;
            case IMapStuffData map:
                snapshot = snapshot with
                {
                    MapPayload = new MapStuffPayload { Data = map.Data.ToImmutableDictionary() },
                };
                break;
            case IStringStuffData str:
                snapshot = snapshot with
                {
                    StringPayload = new StringStuffPayload { Data = [.. str.Data] },
                };
                break;
            case IVoteStuffData vote:
                snapshot = snapshot with
                {
                    VotePayload = new VoteStuffPayload
                    {
                        Data = vote.GetLegacyString(),
                        Result = vote.Result,
                    },
                };
                break;
            case INumberStuffData number:
                snapshot = snapshot with
                {
                    NumberPayload = new NumberStuffPayload { Data = [.. number.Data] },
                };
                break;
            case IHighscoreStuffData highscore:
                snapshot = snapshot with
                {
                    HighscorePayload = new HighscoreStuffPayload
                    {
                        Data = highscore.GetLegacyString(),
                        ScoreType = highscore.ScoreType,
                        ClearType = highscore.ClearType,
                        Scores = highscore
                            .HighscoreData.Select(x =>
                                KeyValuePair.Create(x.Key, x.Value.ToImmutableArray())
                            )
                            .ToImmutableDictionary(),
                    },
                };
                break;
        }

        return snapshot;
    }
}
