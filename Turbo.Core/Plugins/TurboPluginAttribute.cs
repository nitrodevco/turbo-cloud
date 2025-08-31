using System;

namespace Turbo.Core.Plugins;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public class TurboPluginAttribute : Attribute { }
