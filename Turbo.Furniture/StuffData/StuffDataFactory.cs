using System;
using System.Collections.Immutable;
using System.Text.Json;
using Turbo.Primitives.Furniture.Snapshots.StuffData;
using Turbo.Primitives.Furniture.StuffData;

namespace Turbo.Furniture.StuffData;

public sealed class StuffDataFactory : IStuffDataFactory
{
    public IStuffData CreateStuffData(int typeAndFlags)
    {
        var type = (StuffDataType)(typeAndFlags & StuffDataBase.TYPE_MASK);
        var flags = (StuffDataFlags)(typeAndFlags & StuffDataBase.FLAGS_MASK);

        IStuffData data = type switch
        {
            StuffDataType.MapKey => new MapStuffData(),
            StuffDataType.StringKey => new StringStuffData(),
            StuffDataType.VoteKey => new VoteStuffData(),
            StuffDataType.EmptyKey => new EmptyStuffData(),
            StuffDataType.NumberKey => new NumberStuffData(),
            StuffDataType.HighscoreKey => new HighscoreStuffData(),
            StuffDataType.CrackableKey => throw new NotImplementedException(),
            StuffDataType.LegacyKey or _ => new LegacyStuffData(),
        };

        data.SetType(type);

        return data;
    }

    public IStuffData CreateStuffDataFromJson(int typeAndFlags, string jsonString)
    {
        if (string.IsNullOrEmpty(jsonString))
            return CreateStuffData(typeAndFlags);

        var type = (StuffDataType)(typeAndFlags & StuffDataBase.TYPE_MASK);
        var flags = (StuffDataFlags)(typeAndFlags & StuffDataBase.FLAGS_MASK);

        IStuffData? data = type switch
        {
            StuffDataType.MapKey => JsonSerializer.Deserialize<MapStuffData>(jsonString),
            StuffDataType.StringKey => JsonSerializer.Deserialize<StringStuffData>(jsonString),
            StuffDataType.VoteKey => JsonSerializer.Deserialize<VoteStuffData>(jsonString),
            StuffDataType.EmptyKey => JsonSerializer.Deserialize<EmptyStuffData>(jsonString),
            StuffDataType.NumberKey => JsonSerializer.Deserialize<NumberStuffData>(jsonString),
            StuffDataType.HighscoreKey => JsonSerializer.Deserialize<HighscoreStuffData>(
                jsonString
            ),
            StuffDataType.CrackableKey => throw new NotImplementedException(),
            _ => JsonSerializer.Deserialize<LegacyStuffData>(jsonString),
        };

        data ??= JsonSerializer.Deserialize<LegacyStuffData>(jsonString)!;

        data.SetType(type);

        return data;
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
