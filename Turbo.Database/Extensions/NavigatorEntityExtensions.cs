using Turbo.Database.Entities.Navigator;
using Turbo.Primitives.Navigator.Snapshots;

namespace Turbo.Database.Extensions;

public static class NavigatorEntityExtensions
{
    public static NavigatorTopLevelContextSnapshot ToSnapshot(
        this NavigatorTopLevelContextEntity entity
    ) => new() { SearchCode = entity.SearchCode, QuickLinks = [] };

    public static NavigatorFlatCategorySnapshot ToSnapshot(
        this NavigatorFlatCategoryEntity entity
    ) =>
        new()
        {
            Id = entity.Id,
            Name = entity.Name,
            Visible = entity.Visible,
            Automatic = entity.Automatic,
            AutomaticCategoryKey = entity.AutomaticCategory ?? string.Empty,
            GlobalCategoryKey = entity.GlobalCategory ?? string.Empty,
            StaffOnly = entity.StaffOnly,
            MinRank = entity.MinRank,
            OrderNum = entity.OrderNum,
        };

    public static NavigatorEventCategorySnapshot ToSnapshot(
        this NavigatorEventCategoryEntity entity
    ) =>
        new()
        {
            Id = entity.Id,
            Name = entity.Name,
            Visible = entity.Visible,
        };
}
