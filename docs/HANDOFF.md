# Handoff

Session-scoped working note, not authoritative. `NEXT_STEPS.md` and `ARCHITECTURE.md` win on any
disagreement. Delete or rewrite this file freely once its items are done.

Written 2026-09-17.

## Context this replaces

A design discussion about how object interactions should be structured (a matrix of
(actorType, targetType) handlers, versus interaction logic living on each object). Conclusion:
neither, not now. The current hardcoded chain in `TurnResolver.ResolvePlayerAction` stays, and the
abstraction question is deferred to Active roadmap step 9, which now records both axes and the
rejected shapes. Update 2026-09-18: the player side of that question is now scoped by step 5, which
adds attack/dig/move rules. It follows [Claude_Player_Rules_Handoff.md](Claude_Player_Rules_Handoff.md).
The chain stays the entry point. Step 9 still owns effect propagation and any enemy-side sharing.

## Good next tasks for a smaller/cheaper model

Well-specified, localized, covered by existing tests.

1. **Step 7, weapon geometry as data.** `AttackDefinition` already carries `DetectionOffsets`,
   `AttackOffsets`, `MaxTargets` and `StopsAtWalls`, so a spear/dagger/whip is mostly content plus
   the blocking and target-priority rules a non-linear pattern needs. Define those rules explicitly
   first - see `GAME_DESIGN.md`'s weapons section. Tests: `Tests/WeaponAttackTests.cs`.
2. **Step 6, momentum / multiplier.** Self-contained: a counter on run state, reset conditions, and
   a HUD readout. Decide what counts as a "wasted" action before writing any code. Must round-trip
   through `GameSnapshot`/`GameSnapshotRestore` like every other piece of run state.
3. **Step 4 slices 1-3 follow-up.** Manual-playtest fixes in the sandbox toolbar, cursor and
   placement flow, if playtesting turns any up.

## Do not hand these to a smaller model

- **Step 4 slice 4, scenario save/load.** It must never touch the real save slot. The existing
  save path has atomic temp-file replacement, backup rotation and fingerprint validation
  (`RunSaveFileService`, `RunSaveEnvelope`), and the sandbox already relies on `_suppressAutosave`.
  Getting isolation wrong silently destroys a player's run.
- **Step 9 itself.** It is a judgement call about five accumulated mechanics, not an
  implementation task.
- Anything that edits `GameSnapshot` / `GameSnapshotRestore` / `RunSaveSerializer` field-by-field
  round-tripping, or `ContentFingerprinter`.

## Unscheduled proposal, not approved

**Bump damage.** Bumping a wall with no digging tool equipped costs HP, giving the currently free
wall-bump (used to stall enemy timing) a real cost. Pairs with step 6's momentum for the same
reason. Would be a direct edit where `PlayerActionOutcome.Blocked` is returned in
`TurnResolver.ResolvePlayerAction`, plus a way for the debug export to tell a harmless bump from a
damaging one (a new `PlayerActionKind`, or a damage field on `Blocked`), plus coverage in
`Tests/TurnResolverTests.cs`. Not in the roadmap; decide whether it belongs before building it.

## Housekeeping

- Keep `debug_history_export.json` closed/deselected in the editor while working. It is ~36k lines
  and is re-sent as context on every turn.
- Build and test: `dotnet build "New Game Project.csproj"` and
  `dotnet test Tests/TurnDungeon.Tests.csproj` (see `README.md`).
