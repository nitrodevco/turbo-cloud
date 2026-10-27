---
title: "Plugins are compiled from their sources; no plugin assembly is loaded"
date: 2026-10-27
pr: turbo-cloud#463
breaks: yes
who: "every plugin with server code."
---

**What**: the server compiles each plugin's C# sources (manifest `Sources`, default
`server/**/*.cs`) against the plugin surface only: the framework, `Turbo.Contracts`,
`Turbo.Primitives`, `Turbo.Database` and the packages in `docs/plugin-loading.md`. Code that loads
assemblies, calls native code, uses unsafe code, emits code, starts processes or uses the compiler
is **Rejected** (`TPP001`-`TPP006`), and so is a folder holding an executable file. Compiles are
kept, signed per server. The manifest loses `AssemblyFile` and `BuiltAgainst`. The server ships
`plugin-sdk/Turbo.Plugin.props`, which a plugin's project imports to build exactly as the server
compiles (`npm run check`). A GitHub install takes the tag's source archive, not a release asset,
and the link's `Asset` field is now `Folder`.

**Breaks plugins**: A plugin that ships an assembly is Rejected, and one using the server's
internals no longer compiles.

**Action**: `docs/upgrading.md`, all of it.
