using System;

namespace Turbo.Primitives.Settings;

/// <summary>
/// A setting the admin panel shows but can't change: what the panel itself stands on (the
/// database it reads its overrides from, the address it is served at, the silo). A wrong value
/// there would lock staff out of the panel that could put it right, so it stays in
/// <c>appsettings.json</c> and the environment. On a class, every setting of the section.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Property)]
public sealed class StartupSettingAttribute : Attribute;
