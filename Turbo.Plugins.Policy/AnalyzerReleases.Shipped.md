; Shipped analyzer releases
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

## Release 1.0

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
TPP001 | TurboPluginPolicy | Error | Plugins may not load assemblies
TPP002 | TurboPluginPolicy | Error | Plugins may not call native code
TPP003 | TurboPluginPolicy | Error | Plugins may not use unsafe code
TPP004 | TurboPluginPolicy | Error | Plugins may not generate code at run time
TPP005 | TurboPluginPolicy | Error | Plugins may not start processes
TPP006 | TurboPluginPolicy | Error | Plugins may not use the compiler
