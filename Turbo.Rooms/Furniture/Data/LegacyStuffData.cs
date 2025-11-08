using Turbo.Contracts.Enums.Rooms.Furniture.Data;

namespace Turbo.Rooms.Furniture.Data;

public sealed class LegacyStuffData : StuffDataBase
{
    public override StuffDataTypeEnum Type => StuffDataTypeEnum.LegacyKey;

    public string Data { get; set; } = "0";

    public override string GetLegacyString() => Data;

    public override void SetState(string state) => Data = state;

    public override object GetJsonData()
    {
        var parent = base.GetJsonData();

        return new { parent, Data };
    }
}
