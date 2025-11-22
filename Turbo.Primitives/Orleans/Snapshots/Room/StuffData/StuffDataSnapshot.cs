using System.Collections.Immutable;
using System.Linq;
using Orleans;
using Turbo.Primitives.Rooms.Furniture.StuffData;

namespace Turbo.Primitives.Orleans.Snapshots.Room.StuffData;

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
            StuffBitmask = data.GetBitmask(),
            UniqueNumber = data.UniqueNumber,
            UniqueSeries = data.UniqueSeries,
        };

        switch (data)
        {
            case ILegacyStuffData legacy:
                snapshot = snapshot with
                {
                    LegacyPayload = new LegacyStuffPayload
                    {
                        LegacyString = legacy.GetLegacyString(),
                    },
                };
                break;
            case IMapStuffData map:
                snapshot = snapshot with
                {
                    MapPayload = new MapStuffPayload { Data = map.Data.ToImmutableDictionary() },
                };
                break;
            case INumberStuffData number:
                snapshot = snapshot with
                {
                    NumberPayload = new NumberStuffPayload { Data = [.. number.Data] },
                };
                break;
            case IStringStuffData str:
                snapshot = snapshot with
                {
                    StringPayload = new StringStuffPayload { Data = [.. str.Data] },
                };
                break;
        }

        return snapshot;
    }
}
