using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Userdefinedroomevents.Wiredmenu;

/// <summary>
/// Variable Management asks who holds a permanent user variable, one page at a time (AS3
/// <c>WiredGetVariableOwnersPageComposer</c>: variable id, page from 1, page size, sort type
/// <c>wiredmenu.variable_management.sort_by.&lt;n&gt;</c>, user type <c>wiredfurni.params.usertype.&lt;n&gt;</c>
/// or -1 for all).
/// </summary>
public record WiredGetVariableOwnersPageMessage : IMessageEvent
{
    public required string VariableId { get; init; }
    public required int Page { get; init; }
    public required int PageSize { get; init; }
    public required int SortType { get; init; }
    public required int UserTypeFilter { get; init; }
}
