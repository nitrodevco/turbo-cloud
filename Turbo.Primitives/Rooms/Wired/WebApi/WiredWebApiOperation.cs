namespace Turbo.Primitives.Rooms.Wired.WebApi;

/// <summary>
/// The Wired Variables endpoints of the hotel's public Web API (<c>/api/public/rooms/{roomId}/variables...</c>),
/// one per route and method of Habbo's OpenAPI document (tag "Wired Variables").
/// </summary>
public enum WiredWebApiOperation
{
    ListVariables,
    GetValue,
    PutValue,
    PatchValue,
    DeleteValue,
    ListValues,
    CountValues,
    BulkDelete,
    Batch,
    GetGlobal,
    PatchGlobal,
    FindUserProfile,
    GetUserProfile,
    PatchUserProfile,
    DeleteUserProfile,
    GetFurniProfile,
    PatchFurniProfile,
    GetGlobalProfile,
    PatchGlobalProfile,
}
