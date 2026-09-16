using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

// One consumed gameplay turn: what was asked for, what the player and each
// active enemy actually did, and the independent snapshot of everything
// right after. See docs/SAVE_AND_DEBUG_HISTORY.md - "each transition
// records typed outcomes"; PlayerActionOutcome/EnemyActionOutcome already
// are that typed-outcome shape (Scripts/Game/TurnResolver.cs), so a
// transition just bundles them with the command and the resulting state.
public sealed class TurnTransition
{
	public int TurnNumber { get; }
	public Vector2 Direction { get; }
	public PlayerActionOutcome PlayerOutcome { get; }
	public IReadOnlyList<EnemyActionOutcome> EnemyOutcomes { get; }
	public GameSnapshot ResultingSnapshot { get; }

	public TurnTransition(
		int turnNumber,
		Vector2 direction,
		PlayerActionOutcome playerOutcome,
		IReadOnlyList<EnemyActionOutcome> enemyOutcomes,
		GameSnapshot resultingSnapshot)
	{
		TurnNumber = turnNumber;
		Direction = direction;
		PlayerOutcome = playerOutcome ?? throw new ArgumentNullException(nameof(playerOutcome));
		EnemyOutcomes = enemyOutcomes ?? throw new ArgumentNullException(nameof(enemyOutcomes));
		ResultingSnapshot = resultingSnapshot ?? throw new ArgumentNullException(nameof(resultingSnapshot));
	}
}

// The bounded debug ring from docs/SAVE_AND_DEBUG_HISTORY.md: the last 10
// consumed-turn transitions plus the one snapshot immediately before the
// oldest of them, so up to 11 states are always available. At turn 25 the
// retained states are 15..25 - the boundary snapshot IS the evicted
// transition's own resulting snapshot, which is exactly "the state right
// before the new oldest retained transition happened".
public sealed class DebugHistory
{
	private const int MaxTransitions = 10;
	private readonly List<TurnTransition> _transitions = new();

	public GameSnapshot BoundarySnapshot { get; private set; }
	public IReadOnlyList<TurnTransition> Transitions => _transitions;

	public DebugHistory(GameSnapshot initialSnapshot)
	{
		BoundarySnapshot = initialSnapshot ?? throw new ArgumentNullException(nameof(initialSnapshot));
	}

	public void AppendTransition(TurnTransition transition)
	{
		if (transition == null)
			throw new ArgumentNullException(nameof(transition));

		_transitions.Add(transition);

		if (_transitions.Count > MaxTransitions)
		{
			BoundarySnapshot = _transitions[0].ResultingSnapshot;
			_transitions.RemoveAt(0);
		}
	}

	// A new run (including after loading one) starts its own history -
	// retaining history across runs/sessions is not required.
	public void Reset(GameSnapshot initialSnapshot)
	{
		BoundarySnapshot = initialSnapshot ?? throw new ArgumentNullException(nameof(initialSnapshot));
		_transitions.Clear();
	}

	// The state immediately before the last transitionCount transitions -
	// e.g. exporting turns 7..11 (transitionCount 5) returns state 6. Clamped
	// to what is actually retained: asking for at least as many transitions
	// as exist just returns BoundarySnapshot, the same "state right before
	// the oldest retained transition" it always means.
	public GameSnapshot GetSnapshotBefore(int transitionCount)
	{
		int total = _transitions.Count;

		if (transitionCount >= total)
			return BoundarySnapshot;

		return _transitions[total - transitionCount - 1].ResultingSnapshot;
	}

	// The last transitionCount transitions, oldest first, clamped to what is
	// actually retained.
	public IReadOnlyList<TurnTransition> GetLastTransitions(int transitionCount)
	{
		int total = _transitions.Count;
		int take = Math.Clamp(transitionCount, 0, total);

		return _transitions.Skip(total - take).ToList();
	}
}
