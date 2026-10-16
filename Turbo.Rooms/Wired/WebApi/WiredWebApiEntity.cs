using Turbo.Primitives.Furniture;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture;
using Turbo.Primitives.Rooms.Object.Furniture.Wall;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Variables;

namespace Turbo.Rooms.Wired.WebApi;

/// <summary>
/// What a Web API target kind and entity id name: <see cref="ApiId"/> as the API calls it, and
/// <see cref="LiveId"/>, the id its values are kept under in the room (room index, furni id, 0
/// for the room), or -1 when it is not here.
/// </summary>
public sealed record WiredWebApiEntity(
    WiredVariableTargetType TargetType,
    string Kind,
    int ApiId,
    int LiveId,
    string? Name
)
{
    public const string USERS = "users";
    public const string PETS = "pets";
    public const string BOTS = "bots";
    public const string FURNI = "furni";
    public const string FURNI_BC = "furni-bc";
    public const string WALL_ITEMS = "wall-items";
    public const string WALL_ITEMS_BC = "wall-items-bc";
    public const string GLOBAL = "global";

    public bool IsHere => LiveId >= 0;

    public WiredVariableKey KeyFor(FurnitureWiredVariableLogic variable) =>
        new(variable.GetVarSnapshot().VariableId, TargetType, LiveId);

    /// <summary>Whether a target kind belongs to a variable's target (users, pets, bots to a user variable...).</summary>
    public static bool IsKindOf(string kind, WiredVariableTargetType targetType) =>
        targetType switch
        {
            WiredVariableTargetType.User => kind is USERS or PETS or BOTS,
            WiredVariableTargetType.Furni => kind
                is FURNI
                    or FURNI_BC
                    or WALL_ITEMS
                    or WALL_ITEMS_BC,
            WiredVariableTargetType.Global => kind == GLOBAL,
            _ => false,
        };

    public static string KindOf(IRoomItem item)
    {
        var buildersClub = FurniIdBands.IsBuildersClub(item.ObjectId.Value);

        if (item is IRoomWallItem)
            return buildersClub ? WALL_ITEMS_BC : WALL_ITEMS;

        return buildersClub ? FURNI_BC : FURNI;
    }

    /// <summary>A furni's API id: its id, less <c>0x7FFFFFFF - 0xFFFF</c> for a Builders Club one.</summary>
    public static int ApiIdOf(IRoomItem item) =>
        FurniIdBands.IsBuildersClub(item.ObjectId.Value)
            ? item.ObjectId.Value - FurniIdBands.BuildersClubMin
            : item.ObjectId.Value;

    /// <summary>The field a profile names its owner in (<c>WiredUserProfile.user</c>, <c>WiredWallItemBcProfile.wall_item_bc</c>...).</summary>
    public static string ProfileFieldOf(string kind) =>
        kind switch
        {
            USERS => "user",
            PETS => "pet",
            BOTS => "bot",
            FURNI_BC => "furni_bc",
            WALL_ITEMS => "wall_item",
            WALL_ITEMS_BC => "wall_item_bc",
            _ => "furni",
        };
}
