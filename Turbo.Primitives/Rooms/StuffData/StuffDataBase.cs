using System.Text.Json.Serialization;
using Turbo.Contracts.Enums.Rooms.Furniture.Data;

namespace Turbo.Primitives.Rooms.StuffData;

public class StuffDataBase : IStuffData
{
    [JsonIgnore]
    public StuffDataTypeEnum StuffType { get; private set; }

    [JsonPropertyName("U_N")]
    public int UniqueNumber { get; set; } = 0;

    [JsonPropertyName("U_S")]
    public int UniqueSeries { get; set; } = 0;

    public void SetType(StuffDataTypeEnum type) => StuffType = type;

    public bool IsUnique() => UniqueNumber > 0 && UniqueSeries > 0;

    public virtual int GetState() => int.Parse(GetLegacyString());

    public virtual void SetState(string state) { }

    public virtual string GetLegacyString() => string.Empty;

    public virtual object GetJsonData() => new { UniqueNumber, UniqueSeries };
}
