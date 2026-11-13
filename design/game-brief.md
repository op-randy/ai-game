# AI Game — working brief

## Confirmed direction

Create a replayable first-person roguelike/roguelite with dynamic, responsive
movement inspired by the feel of Apex Legends. The user wants levels, level-ups,
skill trees and character progression. Keep the game's own identity and assets;
the reference is movement feel, not a request to reproduce Apex's content.

## Current starting point

The Unity project has a first-person shooting range, rifle and targets, a
separate third-person demo, a humanoid character and character-animation/hair
experiments. These are prototype systems, not a verified roguelite game loop.
Recent user reports include animation/leg quality and hair/head clipping. Do not
describe these as resolved solely because numerical checks passed.

## Proposed first milestone — awaiting implementation approval

Polish the existing first-person movement and shooting range into a small
movement playground: sprinting, jumping, sliding, slide-jumping, air control,
mantling, and an obstacle route with targets. Preserve the rifle and current
assets. Leave skill trees, procedural levels and cosmetics out of this milestone.

Success means the movement is predictable, no camera/collision faults occur on
the route, and the user enjoys moving and shooting for a short play session.
Neither that playtest nor the proposed milestone is complete yet.

## Proposed build order

1. Inspect and stabilize first-person movement, camera and collision behavior.
2. Build and play a small movement course.
3. Add one enemy, player health, death and restart.
4. Add kill XP, level-ups and a small set of three-choice upgrades.
5. Complete one short encounter with a clear end condition.
6. Expand into stages, build synergies and persistent skill-tree options after playtesting.

## Decisions still open

- Guns, melee, abilities, or a specific mix.
- Signature movement ability, if any.
- Run length, level structure and exact death/persistence rules.
- Target release platforms and multiplayer ambitions.
- Asset and performance budgets.

Present choices when they become relevant. Do not block movement inspection on
an entire design document, and do not implement these choices silently.
