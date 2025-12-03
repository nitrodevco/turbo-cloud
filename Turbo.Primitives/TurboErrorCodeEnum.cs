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
    LogicNotFound,
    FurnitureDefinitionNotFound,
    InvalidLogic,
    InvalidFurnitureProductType,
    InvalidSession,
    InvalidMoveTarget,
    NoPermissionToManipulateFurni,
    InvalidFloorItemPlacement,
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
            TurboErrorCodeEnum.LogicNotFound => "The specified furniture logic could not be found.",
            TurboErrorCodeEnum.FurnitureDefinitionNotFound =>
                "The specified furniture definition could not be found.",
            TurboErrorCodeEnum.InvalidLogic => "The logic is not valid.",
            TurboErrorCodeEnum.InvalidFurnitureProductType =>
                "The furniture product type is invalid.",
            TurboErrorCodeEnum.InvalidSession => "The session is invalid.",
            TurboErrorCodeEnum.InvalidMoveTarget => "The move target is invalid.",
            TurboErrorCodeEnum.NoPermissionToManipulateFurni =>
                "You do not have permission to manipulate furniture.",
            TurboErrorCodeEnum.InvalidFloorItemPlacement => "The floor item placement is invalid.",
            _ => "An unknown error occurred.",
        };
}
