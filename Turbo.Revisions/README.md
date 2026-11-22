# Revision Protocol Contract

This folder is the source of truth for the protocol revision mappings: which header is which
message, and how each is read and written.

## What lives here
- Revision definitions:
  - `Revision20260909/Revision20260909.cs`
- Incoming packet parsers:
  - `Revision<id>/Parsers/<Domain>/*MessageParser.cs`
- Outgoing message serializers:
  - `Revision<id>/Serializers/<Domain>/*MessageComposerSerializer.cs`

## Boundary
- `turbo-cloud` owns the revision trees: `Revision<id>/Parsers`, `Revision<id>/Serializers`,
  `Headers.cs` and the registration in `Revision<id>.cs` (`CONTEXT.md`, "Hard boundaries").
- A plugin adds packets of its own through the extension packet registry
  (`docs/plugin-packets.md`), not by editing these folders.
- Handler and grain rules (for example the extended profile flow) are in `AGENTS.md` and
  `CONTEXT.md`.

## Packet addition checklist
When adding packet mappings in `Turbo.Revisions/Revision20260909`:
1. Update `Turbo.Revisions/Revision20260909/Headers.cs`:
   - add/update incoming `MessageEvent` id constants
   - add/update outgoing `MessageComposer` id constants
2. Add parser class under:
   - `Turbo.Revisions/Revision20260909/Parsers/<Domain>/*MessageParser.cs`
3. Add serializer class under:
   - `Turbo.Revisions/Revision20260909/Serializers/<Domain>/*MessageComposerSerializer.cs`
4. Register mappings in:
   - `Turbo.Revisions/Revision20260909/Revision20260909.cs`
   - incoming: `Parsers` dictionary with `MessageEvent` key
   - outgoing: `Serializers` dictionary with composer type + `MessageComposer` id
5. Ensure the required `using` directives are present in `Revision20260909.cs` for new parser/serializer namespaces.

## When adding protocol support
1. Add parser(s) under `Parsers/<Domain>/`.
2. Add serializer(s) under `Serializers/<Domain>/` when needed.
3. Register header/parser/serializer mapping in the relevant `Revision<id>.cs`.
4. Keep naming consistent: `*MessageParser.cs` and `*MessageComposerSerializer.cs`.
5. Keep parsers/serializers deterministic and side-effect free.
