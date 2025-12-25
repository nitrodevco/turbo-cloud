namespace Turbo.Furniture.WiredData;

internal sealed class WiredConditionData : WiredDataBase
{
    public required int QuantifierCode { get; set; }

    public required int QuantifierTypeId { get; set; }
    public required bool IsInvert { get; set; }
}
