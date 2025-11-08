using Turbo.Contracts.Enums.Rooms.Furniture.Data;
using Turbo.Primitives.Rooms;

namespace Turbo.Rooms.Furniture.Data;

public abstract class StuffDataBase : IStuffData
{
    public abstract StuffDataTypeEnum Type { get; }

    public int UniqueNumber { get; set; } = 0;
    public int UniqueSeries { get; set; } = 0;

    public virtual string GetLegacyString() => string.Empty;

    public virtual int GetState() => int.Parse(GetLegacyString());

    public virtual void SetState(string state) { }

    public virtual object GetJsonData() => new { UniqueNumber, UniqueSeries };

    public bool IsUnique() => UniqueNumber > 0 && UniqueSeries > 0;
}
