using Godot;

public partial class Enemy : CharacterBody2D
{
	private const float TileSize = 32.0f;
	private int _health = 2;

	public void TakeDamage(int damage)
	{
		_health -= damage;
		GD.Print($"Enemy health: {_health}");

		if (_health <= 0)
		{
			GD.Print("Enemy defeated!");
			QueueFree();
		}
	}

	public void TakeTurn(Player player)
	{
		Vector2 playerPosition = player.Position;
		Vector2 difference = playerPosition - Position;

		if (difference.IsZeroApprox())
			return;

		Vector2 direction;

		if (Mathf.Abs(difference.X) > Mathf.Abs(difference.Y))
			direction = new Vector2(Mathf.Sign(difference.X), 0);
		else
			direction = new Vector2(0, Mathf.Sign(difference.Y));

		Vector2 nextPosition = Position + direction * TileSize;

		if (nextPosition.IsEqualApprox(playerPosition))
		{
			GD.Print("Enemy attacks player!");
			return;
		}

		Position = nextPosition;
	}
}
