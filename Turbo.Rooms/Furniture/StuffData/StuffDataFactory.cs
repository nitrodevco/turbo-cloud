using System.Text.Json;
using Turbo.Primitives.Rooms.Furniture.StuffData;

namespace Turbo.Rooms.Furniture.StuffData;

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
            StuffDataType.VoteKey => throw new System.NotImplementedException(),
            StuffDataType.EmptyKey => new EmptyStuffData(),
            StuffDataType.NumberKey => new NumberStuffData(),
            StuffDataType.HighscoreKey => throw new System.NotImplementedException(),
            StuffDataType.CrackableKey => throw new System.NotImplementedException(),
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
            StuffDataType.VoteKey => throw new System.NotImplementedException(),
            StuffDataType.EmptyKey => JsonSerializer.Deserialize<EmptyStuffData>(jsonString),
            StuffDataType.NumberKey => JsonSerializer.Deserialize<NumberStuffData>(jsonString),
            StuffDataType.HighscoreKey => throw new System.NotImplementedException(),
            StuffDataType.CrackableKey => throw new System.NotImplementedException(),
            _ => null,
        };

        data ??= JsonSerializer.Deserialize<LegacyStuffData>(jsonString)!;

        data.SetType(type);

        return data;
    }
}
