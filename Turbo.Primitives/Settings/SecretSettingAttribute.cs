using System;

namespace Turbo.Primitives.Settings;

/// <summary>
/// A setting whose value is a secret (a private key, a client secret, a password in a connection
/// string). The admin panel may replace it but never shows it, its history records only that it
/// changed, and no external variable can be linked to it: those are public.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class SecretSettingAttribute : Attribute;
