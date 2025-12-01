using System;
using System.Text.Json;
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
}
