using Godot;
using System;
using System.Collections.Generic;

// Finds the direction of the first step of the shortest wall-avoiding route
// from an enemy to the player, so chase-type behaviors can route around
// obstacles instead of only checking the single cell straight ahead. Grid
// A* (four-directional, uniform step cost), recomputed fresh every prepare
// turn rather than cached/incrementally replanned - at this game's enemy
// counts (tens, not thousands) and room sizes, a full search per prepare
// turn is cheap enough that caching would be premature complexity.
public static class EnemyPathfinding
{
	private static readonly (int DeltaX, int DeltaY)[] Steps =
	{
		(1, 0), (-1, 0), (0, 1), (0, -1)
	};

	// Null when start and goal are the same cell, or no route exists.
	public static Vector2? FindNextStepDirection(
		GridPosition start,
		GridPosition goal,
		Func<GridPosition, bool> isWallAt)
	{
		if (start == goal)
			return null;

		Dictionary<GridPosition, GridPosition> cameFrom = new();
		Dictionary<GridPosition, int> bestCost = new() { [start] = 0 };
		PriorityQueue<GridPosition, int> frontier = new();
		frontier.Enqueue(start, Heuristic(start, goal));
		HashSet<GridPosition> visited = new();

		while (frontier.Count > 0)
		{
			GridPosition current = frontier.Dequeue();

			if (current == goal)
				return FirstStepDirection(cameFrom, start, goal);

			if (!visited.Add(current))
				continue;

			foreach ((int deltaX, int deltaY) in Steps)
			{
				GridPosition next = new(current.X + deltaX, current.Y + deltaY);

				// The player's own cell is always a valid destination even
				// when nothing else could ever stand there as terrain.
				if (next != goal && isWallAt(next))
					continue;

				int cost = bestCost[current] + 1;

				if (bestCost.TryGetValue(next, out int existingCost) && existingCost <= cost)
					continue;

				bestCost[next] = cost;
				cameFrom[next] = current;
				frontier.Enqueue(next, cost + Heuristic(next, goal));
			}
		}

		return null;
	}

	private static int Heuristic(GridPosition a, GridPosition b) =>
		Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);

	private static Vector2? FirstStepDirection(
		Dictionary<GridPosition, GridPosition> cameFrom,
		GridPosition start,
		GridPosition goal)
	{
		GridPosition step = goal;

		while (cameFrom[step] != start)
			step = cameFrom[step];

		return new Vector2(step.X - start.X, step.Y - start.Y);
	}
}
