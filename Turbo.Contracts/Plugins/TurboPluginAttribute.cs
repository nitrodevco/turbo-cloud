using System;

namespace Turbo.Contracts.Plugins;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public class TurboPluginAttribute : Attribute { }
