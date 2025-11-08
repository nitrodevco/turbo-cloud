using System.Text.Json;
using Turbo.Contracts.Enums.Rooms.Furniture.Data;

namespace Turbo.Primitives.Rooms.StuffData;

public sealed class StuffDataFactory
{
    private const int TYPE_MASK = 0xFF;
    private const int FLAGS_MASK = 0xFF00;

    public static int GetBitmaskForStuffData(IStuffData data) =>
        CreateBitmask(
            data.StuffType,
            data.IsUnique() ? StuffDataFlags.Unique : StuffDataFlags.None
        );

    public static int CreateBitmask(StuffDataTypeEnum type, StuffDataFlags flags) =>
        ((int)type & TYPE_MASK) | ((int)flags & FLAGS_MASK);

    public static IStuffData CreateStuffData(int typeAndFlags)
    {
        var type = (StuffDataTypeEnum)(typeAndFlags & TYPE_MASK);
        var flags = (StuffDataFlags)(typeAndFlags & FLAGS_MASK);

        IStuffData data = type switch
        {
            StuffDataTypeEnum.MapKey => new MapStuffData(),
            StuffDataTypeEnum.StringKey => new StringStuffData(),
            StuffDataTypeEnum.VoteKey => throw new System.NotImplementedException(),
            StuffDataTypeEnum.EmptyKey => new EmptyStuffData(),
            StuffDataTypeEnum.NumberKey => new NumberStuffData(),
            StuffDataTypeEnum.HighscoreKey => throw new System.NotImplementedException(),
            StuffDataTypeEnum.CrackableKey => throw new System.NotImplementedException(),
            StuffDataTypeEnum.LegacyKey or _ => new LegacyStuffData(),
        };

        data.SetType(type);

        return data;
    }

    public static IStuffData CreateStuffDataFromJson(int typeAndFlags, string jsonString)
    {
        if (string.IsNullOrEmpty(jsonString))
            return CreateStuffData(typeAndFlags);

        var type = (StuffDataTypeEnum)(typeAndFlags & TYPE_MASK);
        var flags = (StuffDataFlags)(typeAndFlags & FLAGS_MASK);

        IStuffData? data = type switch
        {
            StuffDataTypeEnum.MapKey => JsonSerializer.Deserialize<MapStuffData>(jsonString),
            StuffDataTypeEnum.StringKey => JsonSerializer.Deserialize<StringStuffData>(jsonString),
            StuffDataTypeEnum.VoteKey => throw new System.NotImplementedException(),
            StuffDataTypeEnum.EmptyKey => JsonSerializer.Deserialize<EmptyStuffData>(jsonString),
            StuffDataTypeEnum.NumberKey => JsonSerializer.Deserialize<NumberStuffData>(jsonString),
            StuffDataTypeEnum.HighscoreKey => throw new System.NotImplementedException(),
            StuffDataTypeEnum.CrackableKey => throw new System.NotImplementedException(),
            _ => null,
        };

        data ??= JsonSerializer.Deserialize<LegacyStuffData>(jsonString)!;

        data.SetType(type);

        return data;
    }
}
