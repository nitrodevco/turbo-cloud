---
title: "The server builds client modules from their TypeScript"
date: 2026-10-27
pr: turbo-cloud#461
breaks: yes
who: "every plugin with a client module."
---

**What**: a plugin's module is built by the server from `client/src/index.tsx` (or `index.ts`)
with esbuild, not minified, source map inline, and registered without code. Prebuilt JavaScript is
neither registered nor served (`.js`, `.mjs`, `.cjs` in `wwwroot/` are refused).
`IClientModuleRegistry.Register(key, entry)` takes a `.ts`/`.tsx` entry under `client/src/`. A
module imports only its own files, the SDK, and the client's `react` and `zustand`; anything else
fails the plugin with its `file:line:column`.

**Breaks plugins**: A module shipped as `wwwroot/<key>.js` is no longer served.

**Action**: `docs/upgrading.md`, step 6.
