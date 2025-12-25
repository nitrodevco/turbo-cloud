using System;
using System.Collections.Immutable;
using System.Text.Json;
using Turbo.Furniture.StuffData;
using Turbo.Primitives.Furniture;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Furniture.Snapshots.StuffData;
using Turbo.Primitives.Furniture.StuffData;

namespace Turbo.Furniture.Providers;

public sealed class StuffDataFactory : IStuffDataFactory
{
    public IStuffData CreateStuffData(StuffDataType type)
    {
        return type switch
        {
            StuffDataType.MapKey => new MapStuffData(),
            StuffDataType.StringKey => new StringStuffData(),
            StuffDataType.VoteKey => new VoteStuffData(),
            StuffDataType.EmptyKey => new EmptyStuffData(),
            StuffDataType.NumberKey => new NumberStuffData(),
            StuffDataType.HighscoreKey => new HighscoreStuffData(),
            StuffDataType.CrackableKey or _ => new LegacyStuffData(),
        };
    }

    public IStuffData CreateStuffDataFromJson(StuffDataType type, string? jsonData)
    {
        type = (StuffDataType)((int)type & StuffDataBase.TYPE_MASK);

        if (!string.IsNullOrEmpty(jsonData))
        {
            var reader = new ExtraData(jsonData);

            return CreateStuffDataFromExtraData(type, reader);
        }

        return CreateStuffData(type);
    }

    public IStuffData CreateStuffDataFromExtraData(StuffDataType type, IExtraData extraData)
    {
        type = (StuffDataType)((int)type & StuffDataBase.TYPE_MASK);

        if (extraData.TryGetSection("stuff", out var stuffElement))
        {
            return type switch
            {
                StuffDataType.MapKey => stuffElement.Deserialize<MapStuffData>()!,
                StuffDataType.StringKey => stuffElement.Deserialize<StringStuffData>()!,
                StuffDataType.VoteKey => stuffElement.Deserialize<VoteStuffData>()!,
                StuffDataType.EmptyKey => stuffElement.Deserialize<EmptyStuffData>()!,
                StuffDataType.NumberKey => stuffElement.Deserialize<NumberStuffData>()!,
                StuffDataType.HighscoreKey => stuffElement.Deserialize<HighscoreStuffData>()!,
                StuffDataType.CrackableKey or _ => stuffElement.Deserialize<LegacyStuffData>()!,
            };
        }

        return CreateStuffData(type);
    }

    public StuffDataSnapshot FromStuffData(IStuffData data)
    {
        var bitmask = data.GetBitmask();
        var uniqueNumber = data.UniqueNumber;
        var uniqueSeries = data.UniqueSeries;

        return data switch
        {
            ILegacyStuffData legacy => new LegacyStuffSnapshot
            {
                StuffBitmask = bitmask,
                UniqueNumber = uniqueNumber,
                UniqueSeries = uniqueSeries,
                Data = legacy.GetLegacyString(),
            },
            IMapStuffData map => new MapStuffSnapshot
            {
                StuffBitmask = bitmask,
                UniqueNumber = uniqueNumber,
                UniqueSeries = uniqueSeries,
                Data = map.Data.ToImmutableDictionary(),
            },
            INumberStuffData number => new NumberStuffSnapshot
            {
                StuffBitmask = bitmask,
                UniqueNumber = uniqueNumber,
                UniqueSeries = uniqueSeries,
                Data = [.. number.Data],
            },
            IStringStuffData str => new StringStuffSnapshot
            {
                StuffBitmask = bitmask,
                UniqueNumber = uniqueNumber,
                UniqueSeries = uniqueSeries,
                Data = [.. str.Data],
            },
            IVoteStuffData vote => new VoteStuffSnapshot
            {
                StuffBitmask = bitmask,
                UniqueNumber = uniqueNumber,
                UniqueSeries = uniqueSeries,
                Data = vote.GetLegacyString(),
                Result = vote.Result,
            },
            IHighscoreStuffData highscore => new HighscoreStuffSnapshot
            {
                StuffBitmask = bitmask,
                UniqueNumber = uniqueNumber,
                UniqueSeries = uniqueSeries,
                Data = highscore.GetLegacyString(),
                ScoreType = highscore.ScoreType,
                ClearType = highscore.ClearType,
                Scores = highscore.HighscoreData.ToImmutableDictionary(
                    kv => kv.Key,
                    kv => kv.Value.ToImmutableArray()
                ),
            },
            IEmptyStuffData empty => new EmptyStuffSnapshot
            {
                StuffBitmask = bitmask,
                UniqueNumber = uniqueNumber,
                UniqueSeries = uniqueSeries,
            },
            _ => throw new ArgumentOutOfRangeException(nameof(data), "Unknown stuff data type"),
        };
    }
}
