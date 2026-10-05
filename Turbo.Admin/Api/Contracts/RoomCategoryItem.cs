namespace Turbo.Admin.Api.Contracts;

/// <summary>A navigator category a room may be put in.</summary>
public sealed record RoomCategoryItem(int Id, string Name, bool Visible, bool StaffOnly);
