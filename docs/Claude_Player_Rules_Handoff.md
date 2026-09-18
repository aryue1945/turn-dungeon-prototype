# Player rules: verified review and Claude update request

## Review baseline

Repository: `aryue1945/turn-dungeon-prototype`.
Reviewed pushed `main` at `be1eebaa270fcd39a70402c9dd3ab2396307a810`, dated 2026-09-18 UTC, via GitHub file reads. This is a source review, not a local git pull or an executed build/test run. Local/unpushed changes are not included. Latest commit removes Space/Wait; do not restore it incidentally.

## Request to Claude

Please check your current branch and worktree, read repository instructions, and compare with this baseline before updating anything. Preserve uncommitted work. First update the design/implementation plan and relevant docs to reflect the direction below. Distinguish existing behavior, proposed changes, and unresolved gameplay choices. Do not rewrite the game or implement all example content automatically. Report the smallest first implementation slice and decisions needed before coding.

## Agreed direction

- Player directional input follows a fixed sequence: attempt attack first; if no attack is available, handle the terrain ahead with dig rules; if blocked, stop; otherwise use movement rules to resolve movement.
- Keep enemy decision-making separate, local to its existing behavior implementations. Reuse shared combat/geometry where appropriate without converting enemy AI into player rules.
- Three scoped rule categories: attack, dig, move. No universal event engine, object-pair interaction matrix, global object scan, ECS, or generic effect language.
- Rules remain associated with their source: equipped weapon/tool/other equipped items, inherent player abilities, or active temporary statuses. Unequipped inventory does not contribute.
- A small collection method gathers active rules for the relevant category. The resolver consumes those rules; it need not identify mushroom/ring/weapon types individually. Do not build a new inventory system solely to supply this method: adapt the current weapon/tool slots first.
- Rules can stack or override. Each rule exposes priority as inspectable metadata, not a hidden side effect inside its function. Priority establishes order; the rule must separately define whether it adds, replaces, or cancels. Specify a deterministic tie-breaker.
- Relative positions use `(Forward, Right)`: front `(1,0)`, front-left `(1,-1)`, front two/left one `(2,-1)`. An origin plus cardinal facing translates these into grid cells. Origin may be the player or an interacted wall.
- Extend existing debug history with rule source, order, condition outcome, changes, and final affected targets. No separate replay engine.

## What the latest source already implements

| Requirement | Existing code | Required change |
| --- | --- | --- |
| Attack before dig before movement | `Scripts/Game/TurnResolver.cs`, `ResolvePlayerAction` | Retain this entry point and precedence; add scoped rule collection where needed. |
| Relative forward/right coordinates | `Scripts/Combat/AttackDefinition.cs`, `AttackOffset` | Reuse; do not introduce a duplicate offset type. |
| Facing-relative translation | `Scripts/Combat/AttackResolver.cs`, private `GetPatternPosition` | Extract a shared helper only when digging/other consumers need it. |
| Separate activation and hit geometry | `DetectionOffsets` and `AttackOffsets` | Preserve distinction; activation must honor walls. |
| Multiple targets, wall stop, source exclusion | `AttackResolver`, `MaxTargets`, `StopsAtWalls`, `AllExceptAttacker` | Reuse actor targeting; clarify area versus ray blocking. |
| Equipment identity | `IEquipment`, `WeaponDefinition`, `DiggingToolDefinition` | Add scoped rule providers without replacing stable IDs. |
| Per-actor runtime state | `ActorState`, `AttackState` | Add status instances only when implementing a concrete status. |
| Attack diagnostics | `AttackExecutionDetail`, debug-history export | Extend with composition trace and dig/move consequences as needed. |

`Player` currently has one Weapon and one DiggingTool, not the previously illustrated `EquippedItems`, `PersonalAttackRules`, or `ActiveStatuses` collections. Those earlier examples were proposals, not existing APIs. `StatusEffectId` is explicitly not applied by the current attack resolver.

## Review findings and necessary safeguards

1. **Shared definitions must stay unchanged.** Built-in weapons are static shared definitions. Build a fresh per-action resolved description for modifiers. Never add mushroom damage into the shared definition or mutate shared offset arrays. Test that a second actor/action and the post-expiration weapon retain base values.
2. **Do not collapse every outcome into a boolean.** Preserve NoAttack/Preparing/Attacked. If introducing cancellation, distinguish a terminal blocked/cancelled action from no applicable attack. Only genuine no-attack should fall through to digging/movement. Document whether each outcome consumes a turn.
3. **Priority alone is insufficient.** Specify base construction before additive modifiers; define replacement scope and tie ordering. Example: laser base 2 plus mushroom 3 yields 5, not 2 because the base was applied last. Two geometry replacements need an explicit winner. Cancellation must not be unintentionally undone by a later modifier.
4. **Geometry blocking is currently list-wide.** Both detection and execution use `break` at a wall. This fits an ordered ray, but one blocked cell would suppress subsequent unrelated cells in a surrounding-area list. Distinguish ray stopping from area-cell filtering; retain existing sword behavior. Do not silently choose area line-of-sight semantics.
5. **Move rules are not necessarily all after-move hooks.** A rule changing reach or permission must run before committing movement; a trail or landing effect runs after a successful move. Keep the move category, but specify this distinction when a concrete example needs it. Failed movement must not fire successful-move effects.
6. **Temporary status duration needs a defined unit.** The user wrote a mushroom lasting “20 room”; prior assistant examples assumed 20 turns. Confirm rooms versus turns before implementation. Define refresh/stack behavior and expiry boundary. Persist stable status IDs and runtime values, not rule objects/delegates; cover normal saves and scenario saves.
7. **Prepared attacks need an explicit modifier policy.** Preserve existing preparation and locked direction. Decide whether modifiers are captured on preparation or recomputed at execution, especially if a status expires meanwhile. Do not reset preparation by calling `AttackState.Equip` every action.
8. **Digger splash is not just an attack offset change.** `DigResolver` currently only damages terrain; `ICombatant` only covers actors. Reuse existing terrain and actor damage paths. Do not claim arbitrary props are supported without adding a concrete representation. Exclude the source player by identity even when the wall is the pattern origin.
9. **Content validation must cover new gameplay data.** `ContentFingerprinter.DescribeAttack` currently omits offsets, Knockback, and StatusEffectId. New geometry/rule definitions would also need coverage. Assess compatibility effects explicitly rather than silently invalidating or weakening existing save validation.

## Concrete acceptance examples

- Laser: requires at least one valid detected enemy before the first wall; after activation, hits every eligible enemy along the beam until the wall/map boundary. An enemy behind the wall cannot activate it. Keep activation geometry independently configurable if the intended trigger is adjacent-only.
- Digger: digs the wall ahead and damages nearby eligible objects around the wall, excluding the player. Confirm four versus eight neighbors, wall occlusion, whether splash occurs on every successful dig or only destruction, and which objects are damageable. No movement into the wall as part of the existing dig action.
- Mushroom: contributes a temporary attack rule, stacks with base equipment according to the declared operation, and disappears exactly at the defined duration boundary without changing the base weapon.

## Suggested incremental plan

1. Update `docs/GAME_DESIGN.md`, `docs/NEXT_STEPS.md`, `docs/HANDOFF.md`; update `docs/ARCHITECTURE.md` only as needed and label unimplemented portions. The existing handoff/roadmap deliberately defer abstraction to step 8: record this new scoped proposal explicitly instead of leaving conflicting instructions.
2. Preserve existing sword/hammer/shovel behavior; add regression coverage and reuse/extract current geometry helper. Cover all four cardinal directions and nonzero sideways offsets.
3. Add the smallest attack-rule composition seam, proven with a test-only additive modifier and replacement-order tests. Do not build every future slot or rule category implementation at once.
4. Implement one selected real status or digger mechanic after resolving its gameplay choices, including persistence, diagnostics, and definition fingerprint implications.
5. Extend dig/move composition only to meet selected concrete mechanics; leave enemy behavior and unrelated backlog work unchanged.

## Verification required from Claude

- Run `dotnet build "New Game Project.csproj"` and `dotnet test Tests/TurnDungeon.Tests.csproj` for code changes; report actual results.
- Test existing weapons, digging, preparation, door opening, turn completion, and unchanged enemy behavior.
- Test rule source inclusion/exclusion, deterministic ties, additive versus replacement order, no shared-definition mutation, and cancellation without fall-through.
- Test laser activation versus hit range/walls; relative rotations; ray versus area blocking; dig splash/source exclusion; no duplicate target damage within one ordinary hit pattern.
- Round-trip any new runtime status/prepared-plan data through capture, restore, serializer, scenario storage, and debug history. Retain save-slot isolation.
- For a docs-only first update, verify links and proposal/implemented labels; do not report unrun tests as passing.

Finish with files changed, behavior preserved, decisions still open, tests actually run, and the proposed first coding slice. Keep implementation incremental and do not merge or deploy automatically.
