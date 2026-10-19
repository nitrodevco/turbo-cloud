using System;

namespace Turbo.Admin.Api.Contracts;

/// <summary>
/// A restriction that holds on a player now (a silence, a trade lock): until when, or null for
/// good.
/// </summary>
public sealed record PlayerRestrictionItem(DateTime? UntilUtc);
