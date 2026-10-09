using System.Collections.Generic;

namespace Turbo.Admin.Api.Contracts;

/// <summary>
/// Changes to the hotel view saved as one: <c>landing.view.*</c> variables by key, each a JSON value
/// or null to remove it, and texts by key, each a value or null to remove it.
/// </summary>
public sealed record HotelViewSaveRequest(
    Dictionary<string, string?>? Variables,
    Dictionary<string, string?>? Texts
);
