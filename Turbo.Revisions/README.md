# Revision Protocol Contract

This folder is the source of truth for plugin protocol revision mappings.

## What lives here
- Revision definitions:
  - `Revision20260701/Revision20260701.cs`
- Incoming packet parsers:
  - `Revision<id>/Parsers/<Domain>/*MessageParser.cs`
- Outgoing message serializers:
  - `Revision<id>/Serializers/<Domain>/*MessageComposerSerializer.cs`

## Cross-repo boundary (important)
- `turbo-sample-plugin` owns revision parser/serializer code.
- `turbo-cloud` does not own `Revision<id>/Parsers` or `Revision<id>/Serializers`.
- `turbo-cloud` owns runtime host/domain handlers; plugin revision folders own packet read/write mappings.
- For handler/grain architecture rules (for example extended profile flow), follow:
  - `../turbo-cloud/AGENTS.md`
  - `../turbo-cloud/CONTEXT.md`
- For reusable AI task recipes/prompt contract, follow `../turbo-cloud/AGENTS.md`.

## When adding protocol support
1. Add parser(s) under `Parsers/<Domain>/`.
2. Add serializer(s) under `Serializers/<Domain>/` when needed.
3. Register header/parser/serializer mapping in the relevant `Revision<id>.cs`.
4. Keep naming consistent: `*MessageParser.cs` and `*MessageComposerSerializer.cs`.
5. Keep parsers/serializers deterministic and side-effect free.
