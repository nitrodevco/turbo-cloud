namespace Turbo.Contracts.Enums;

public enum TurboErrorCodeEnum
{
    Unknown = 0,
    PlayerNotFound,
    RoomNotFound,
    ModelNotFound,
    MapNotLoaded,
    TileOutOfBounds,
    FloorItemNotFound,
    WallItemNotFound,
    LogicNotFound,
    FurnitureDefinitionNotFound,
    InvalidFloorLogic,
    InvalidWallLogic,
    InvalidFurnitureProductType,
    InvalidSession,
}

public static class ErrorCodeExtensions
{
    public static string ToDefaultMessage(this TurboErrorCodeEnum code) =>
        code switch
        {
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
            TurboErrorCodeEnum.InvalidFloorLogic =>
                "The furniture logic is not valid for floor items.",
            TurboErrorCodeEnum.InvalidWallLogic =>
                "The furniture logic is not valid for wall items.",
            TurboErrorCodeEnum.InvalidFurnitureProductType =>
                "The furniture product type is invalid.",
            TurboErrorCodeEnum.InvalidSession => "The session is invalid.",
            _ => "An unknown error occurred.",
        };
}
