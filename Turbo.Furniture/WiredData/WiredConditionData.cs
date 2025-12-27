using Turbo.Primitives.Furniture.Snapshots.WiredData;

namespace Turbo.Furniture.WiredData;

internal sealed class WiredConditionData : WiredDataBase
{
    public int QuantifierCode { get; set; }

    public int QuantifierTypeId { get; set; }
    public bool IsInvert { get; set; }

    protected override WiredDataSnapshot BuildSnapshot() =>
        new WiredDataConditionSnapshot()
        {
            FurniLimit = FurniLimit,
            StuffIds = StuffIds,
            StuffTypeId = 0,
            Id = 0,
            StringParam = StringParam,
            IntParams = IntParams,
            VariableIds = [],
            FurniSourceTypes = FurniSources,
            UserSourceTypes = PlayerSources,
            Code = WiredCode,
            AdvancedMode = false,
            AmountFurniSelections = [],
            AllowWallFurni = false,
            AllowedFurniSources = [],
            AllowedUserSources = [],
            DefaultFurniSources = [],
            DefaultUserSources = [],
            QuantifierCode = QuantifierCode,
            QuantifierType = QuantifierTypeId,
            IsInvert = IsInvert,
        };
}
