using System.Collections.Generic;

namespace Turbo.Plugins;

public record PluginLoadPolicy(
    IEnumerable<string>? ShareAllowPatterns,
    IEnumerable<string>? ShareDenyPatterns
);
