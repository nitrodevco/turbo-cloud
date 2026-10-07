namespace Turbo.Admin.Api.Contracts;

/// <summary>The furnidata fields to put Habbo's values back in (<c>canputstuffon</c>, <c>recyclable</c>, ...).</summary>
public sealed record HabboValuesRequest(string[]? Fields);
