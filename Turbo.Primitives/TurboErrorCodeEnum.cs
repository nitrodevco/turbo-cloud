namespace Turbo.Primitives;

public enum TurboErrorCodeEnum
{
    Unknown = 0,
    AvatarNotFound,
    PlayerNotFound,
    RoomNotFound,
    ModelNotFound,
    MapNotLoaded,
    TileOutOfBounds,
    FloorItemNotFound,
    WallItemNotFound,
    FurnitureDefinitionNotFound,
    CatalogProductNotFound,
    InvalidLogic,
    InvalidWired,
    InvalidFurnitureProductType,
    InvalidSession,
    InvalidMoveTarget,
    NoPermissionToPlaceFurni,
    NoPermissionToManipulateFurni,
    NoPermissionToReadWired,
    NoPermissionToModifyWired,
    WiredFloorItemLimitReached,
    WiredWallItemLimitReached,
    WiredPermanentVariableLimitReached,
    WiredVariableFxLimitReached,
    PetNotFound,
    BotNotFound,
}

public static class ErrorCodeExtensions
{
    public static string ToDefaultMessage(this TurboErrorCodeEnum code) =>
        code switch
        {
            TurboErrorCodeEnum.AvatarNotFound => "The specified avatar could not be found.",
            TurboErrorCodeEnum.PlayerNotFound => "The specified player could not be found.",
            TurboErrorCodeEnum.RoomNotFound => "The specified room could not be found.",
            TurboErrorCodeEnum.ModelNotFound => "The room model could not be found.",
            TurboErrorCodeEnum.MapNotLoaded => "The room map is not loaded.",
            TurboErrorCodeEnum.TileOutOfBounds => "The tile index is out of bounds.",
            TurboErrorCodeEnum.FloorItemNotFound => "The specified floor item could not be found.",
            TurboErrorCodeEnum.WallItemNotFound => "The specified wall item could not be found.",
            TurboErrorCodeEnum.FurnitureDefinitionNotFound =>
                "The specified furniture definition could not be found.",
            TurboErrorCodeEnum.CatalogProductNotFound =>
                "The specified catalog product could not be found.",
            TurboErrorCodeEnum.InvalidLogic => "The logic is not valid.",
            TurboErrorCodeEnum.InvalidWired => "The wired definition is not valid.",
            TurboErrorCodeEnum.InvalidFurnitureProductType =>
                "The furniture product type is invalid.",
            TurboErrorCodeEnum.InvalidSession => "The session is invalid.",
            TurboErrorCodeEnum.InvalidMoveTarget => "The move target is invalid.",
            TurboErrorCodeEnum.NoPermissionToPlaceFurni =>
                "You do not have permission to place furniture.",
            TurboErrorCodeEnum.NoPermissionToManipulateFurni =>
                "You do not have permission to manipulate furniture.",
            TurboErrorCodeEnum.NoPermissionToReadWired =>
                "You do not have permission to read wired in this room.",
            TurboErrorCodeEnum.NoPermissionToModifyWired =>
                "You do not have permission to modify wired in this room.",
            TurboErrorCodeEnum.WiredFloorItemLimitReached =>
                "This room has reached its wired floor furniture limit.",
            TurboErrorCodeEnum.WiredWallItemLimitReached =>
                "This room has reached its wired wall furniture limit.",
            TurboErrorCodeEnum.WiredPermanentVariableLimitReached =>
                "This room has reached its permanent wired variable limit.",
            TurboErrorCodeEnum.WiredVariableFxLimitReached =>
                "This room has reached its variable fx limit.",
            TurboErrorCodeEnum.PetNotFound => "The specified pet could not be found.",
            TurboErrorCodeEnum.BotNotFound => "The specified bot could not be found.",
            _ => "An unknown error occurred.",
        };
}
