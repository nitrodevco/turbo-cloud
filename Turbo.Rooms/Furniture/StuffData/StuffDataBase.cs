using System.Text.Json.Serialization;
using Turbo.Primitives.Rooms.Furniture.StuffData;

namespace Turbo.Rooms.Furniture.StuffData;

public class StuffDataBase : IStuffData
{
    public const int TYPE_MASK = 0xFF;
    public const int FLAGS_MASK = 0xFF00;

    public static int CreateBitmask(StuffDataType type, StuffDataFlags flags) =>
        ((int)type & TYPE_MASK) | ((int)flags & FLAGS_MASK);

    [JsonIgnore]
    public StuffDataType StuffType { get; private set; }

    [JsonPropertyName("U_N")]
    public int UniqueNumber { get; set; } = 0;

    [JsonPropertyName("U_S")]
    public int UniqueSeries { get; set; } = 0;

    public int GetBitmask() =>
        CreateBitmask(StuffType, IsUnique() ? StuffDataFlags.Unique : StuffDataFlags.None);

    public void SetType(StuffDataType type) => StuffType = type;

    public bool IsUnique() => UniqueNumber > 0 && UniqueSeries > 0;

    public virtual int GetState() => int.Parse(GetLegacyString());

    public virtual void SetState(string state) { }

    public virtual string GetLegacyString() => string.Empty;
}
