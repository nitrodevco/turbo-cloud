using System;

namespace Turbo.Primitives.Settings;

/// <summary>
/// A text setting that holds a web address (<c>https://...</c>), or nothing: the admin panel
/// refuses anything else, so a value typed wrong can't leave it quietly unset. Placeholders the
/// setting fills in (<c>{name}</c>, <c>%libname%</c>) may stand anywhere in it.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class UrlSettingAttribute : Attribute;
