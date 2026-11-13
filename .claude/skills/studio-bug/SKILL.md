---
name: studio-bug
description: Reproduce and diagnose a reported Unity gameplay, animation, camera or physics bug before a focused fix. Use when something is broken, bugged, jittering, clipping or regressed; require evidence of the actual symptom.
user-invocable: true
---

# Reproduce before patching

Adapted from upstream bug-report's reproduction/evidence fields and the local
project's visual regressions. Read root `AGENTS.md` first.

1. Establish expected versus actual behavior, scene, input sequence, frequency
   and relevant view. A cropped head screenshot cannot establish a leg defect.
   Ask for a full-body view or a short recording when needed; do not invent what
   is outside the image.
2. Check Git status and construct the smallest repeatable check that exercises
   the real symptom. Prefer an existing scene/test seam. Before diagnosing the
   cause, show a failing run or an observed failing pose/sequence. If access or
   evidence is insufficient, state what could not be assessed and ask for it.
3. Minimize the failing scenario without changing the symptom. Separate camera,
   controller, animator, avatar, imported clip and secondary physics where relevant.
4. Present a short ranked list of falsifiable causes. Test one change at a time
   against the failing check rather than changing several physics/animation values.
5. When a fix is requested, make the smallest in-scope correction and add a
   regression test at the real seam if possible. Preserve assets and GUIDs.
   If this is report-only, stop with the findings and proposed fix.
6. Re-run the original scenario and inspect visual output on the actual character.
   Tests of state existence or a synthetic hair chain do not reproduce a broken
   leg pose, visible scalp gap, or unstable player motion.
7. Remove temporary instrumentation. Report cause, exact fix, verification and
   remaining uncertainty. Do not mark a fix verified when the original check
   could not run. Do not publish an external issue or close a bug automatically.

A saved bug report, when requested, records preconditions, input/scene steps,
expected/actual behavior, environment/version, reproducibility, evidence, status
and an explicitly labeled hypothesis or confirmed cause.
