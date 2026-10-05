namespace Turbo.Admin.Api.Contracts;

/// <summary>
/// Something to do to the whole hotel from the panel: <c>alert</c> (a pop-up for everyone online),
/// <c>maintenance</c> or <c>shutdown</c> after <c>Minutes</c>, <c>maintenance-off</c> or
/// <c>shutdown-cancel</c>. <c>Message</c> is the alert, or the reason given with a countdown.
/// </summary>
public sealed record HotelActionRequest(string? Action, int? Minutes, string? Message);
