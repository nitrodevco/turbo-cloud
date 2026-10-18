using System;

namespace Turbo.Primitives.Settings;

/// <summary>
/// A setting the server reads as it uses it (through <c>IOptionsMonitor&lt;T&gt;.CurrentValue</c>,
/// never kept from the start), so a change in the admin panel applies at once instead of after a
/// restart. Mark a property only when every reader of it reads it that way. On a class, every
/// setting of the section.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Property)]
public sealed class LiveSettingAttribute : Attribute;
