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
            _ => "An unknown error occurred.",
        };
}
