# Next Steps

Status: proposed roadmap awaiting approval.

## 1. Unify terrain authority

Remove enemy wall-node scanning.
Use DungeonMap for all actor terrain checks.

Acceptance:
- Player and enemies agree on blocked cells.
- Empty and out-of-bounds cells block movement.
- Existing turn timing remains unchanged.

## 2. Extract actor state

Add authoritative integer positions, health, facing, and action state.
Convert combat calculations from pixels to cells.

Acceptance:
- Actor rules work without Godot nodes.
- Visual positions do not affect targeting or occupancy.
- Existing weapon tests are preserved in grid coordinates.

## 3. Extract complete turns

Introduce GameState, TurnResolver, and a small EnemyBrain.
Keep sequential enemy execution and current behavior.

Acceptance:
- Complete turns run in NUnit.
- Dead enemies never act.
- Slow-chaser timing and turning behavior are preserved.
- Wall bumps retain their current turn cost.

## 4. Complete terrain interactions

Add closed/open door state, digging, changed-cell rendering,
and a distinct breakable-wall appearance.

Proposed behavior:
- Door entry opens and moves in one action.
- Digging consumes a turn without movement.

Acceptance:
- Map state and graphics agree.
- Both actors see changes immediately.
- Solid walls remain indestructible.

## 5. Complete keyboard flow and feedback

Add initial weapon-menu focus, arrow navigation, confirmation,
and a wait command.

Acceptance:
- Selection works without a mouse.
- One input produces at most one gameplay command.
- Menu confirmation does not also move the player.
- Presentation does not modify simulation state.

## 6. Reproduce full encounters

Record a run seed/configuration and seed initial spawning.
Expand generation tests across a fixed seed set.

Acceptance:
- Same setup reproduces terrain and actors.
- Spawns are legal, unique, and in permitted zones.
- Required areas remain accessible through legal interactions.

## 7. Implement floor objectives

Separate room clearing from floor and run completion.
Give the exit a gameplay function.

Acceptance:
- Killing the last enemy does not bypass the intended exit flow.
- Each transition occurs once.
- Previous-floor state is removed correctly.

## 8. Expand tactical content

Add one non-linear weapon and one enemy that navigates obstacles.

Acceptance:
- Attack blocking and target order are explicit.
- Navigation respects current terrain.
- Existing enemy identities remain unchanged.

## 9. Add difficulty and run rewards

Introduce a small RunConfig and one useful reward/spending loop.

Acceptance:
- Modifiers are applied once.
- Shared definitions are not mutated.
- Rewards cannot be granted twice.

## 10. Add persistent progression

Save one unlock and one cosmetic purchase first.

Acceptance:
- Save/load preserves ownership and balances.
- Missing fields receive defined defaults.
- Temporary run effects do not become permanent.

## Change discipline

- Keep each milestone reviewable.
- Separate behavior-preserving refactors from gameplay changes.
- Add tests for rules and regressions, not private implementation details.
- Update documentation when implemented behavior changes.
- Keep the existing generator until a concrete design need exceeds it.