using System;

namespace Turbo.Admin.Api.Contracts;

/// <summary>A login step the server saw.</summary>
public sealed record ClientLoginRow(DateTime At, string Step, int MsSinceConnect);
