using System.Linq;
using Orleans;
using Turbo.Primitives.Rooms.StuffData;

namespace Turbo.Primitives.Snapshots.Rooms.StuffData;

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
    public NumberStuffPayload? NumberPayload { get; init; }

    [Id(6)]
    public StringStuffPayload? StringPayload { get; init; }

    public static StuffDataSnapshot FromStuffData(IStuffData data)
    {
        var snapshot = new StuffDataSnapshot
        {
            StuffBitmask = StuffDataFactory.GetBitmaskForStuffData(data),
            UniqueNumber = data.UniqueNumber,
            UniqueSeries = data.UniqueSeries,
        };

        switch (data)
        {
            case LegacyStuffData legacy:
                snapshot = snapshot with
                {
                    LegacyPayload = new LegacyStuffPayload
                    {
                        LegacyString = legacy.GetLegacyString(),
                    },
                };
                break;
            case MapStuffData map:
                snapshot = snapshot with
                {
                    MapPayload = new MapStuffPayload
                    {
                        Data = [.. map.Data.Select(kvp => (kvp.Key, kvp.Value))],
                    },
                };
                break;
            case NumberStuffData number:
                snapshot = snapshot with
                {
                    NumberPayload = new NumberStuffPayload { Data = [.. number.Data] },
                };
                break;
            case StringStuffData str:
                snapshot = snapshot with
                {
                    StringPayload = new StringStuffPayload { Data = [.. str.Data] },
                };
                break;
        }

        return snapshot;
    }
}
