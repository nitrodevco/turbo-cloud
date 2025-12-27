using Turbo.Primitives.Furniture.Snapshots.WiredData;

namespace Turbo.Furniture.WiredData;

internal sealed class WiredAddonData : WiredDataBase
{
    protected override WiredDataSnapshot BuildSnapshot() =>
        new WiredDataAddonSnapshot()
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
        };
}
