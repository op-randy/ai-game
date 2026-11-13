---
name: studio-art
description: Assess Unity character rigs, animation retargeting, hair, materials and asset integration against a visual reference. Use for technical-art audits and visible character issues; distinguish audit requests from permission to replace assets.
user-invocable: true
---

# Technical-art assessment

Adapted from upstream asset-audit and technical-artist. Read root `AGENTS.md`,
the user's reference/request and `tools/studio/roles/technical-artist.md`.

1. Establish the requested look or behavior, what must stay, and whether this is
   an audit, a tuning pass or an approved replacement. Preserve original sources.
2. Inspect the real asset/rig and importer settings, not only a generated mock.
   For animation: check avatar mapping, bind/rest pose, scale, root motion,
   retargeting, feet/knees and transitions. Treat generated muscle-offset clips
   as provisional until visually assessed; do not repeatedly patch curves blind.
3. For hair: inspect attachment, silhouette, deformation weights/control frames,
   frame-rate behavior, collision approximations and the actual rendered surface.
   A numerical body proxy does not guarantee cape, helmet or hand clearance.
4. Measure budgets only if a budget and measuring tool exist. An absent asset
   reference in code does not make a Unity asset orphaned; inspect GUID/scene
   references through appropriate editor tooling before suggesting deletion.
5. Present the smallest correction and tradeoffs. Ask before buying/downloading
   assets, changing licenses, importing plugins, replacing geometry/rigs or doing
   unrelated aesthetic work. Use authored compatible assets when appropriate;
   do not promise photoreal hair from a procedural tube.
6. For an approved edit, compare before/after on the same model, pose, camera and
   lighting, then check motion/transitions in the actual scene. Keep views that
   expose the issue rather than hiding it with a convenient crop.
7. Report measured/observed findings separately from subjective judgments and
   untested claims. Ask for human visual approval; do not infer it from a test pass.

Do not change gameplay during an asset audit. Do not declare the visual issue
fixed merely because a mesh imports, a renderer exists, or a smoke test passes.
