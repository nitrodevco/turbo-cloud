# Revision20260112 Mapping Checklist

Use this checklist when adding or updating packets in this revision.

## Required files to touch
1. `Headers.cs`
   - add incoming ids to `MessageEvent`
   - add outgoing ids to `MessageComposer`
2. Parser files
   - create/update `Parsers/<Domain>/*MessageParser.cs` for incoming packets
3. Serializer files
   - create/update `Serializers/<Domain>/*MessageComposerSerializer.cs` for outgoing packets
4. Revision registration
   - update `Revision20260112.cs`:
     - `Parsers` dictionary for incoming event id -> parser mapping
     - `Serializers` dictionary for composer type -> serializer mapping

## Consistency checks
- Keep parser/serializer names aligned with message names.
- Ensure `Revision20260112.cs` contains required `using` directives for new domains.
- Keep parsers and serializers deterministic and side-effect free.
