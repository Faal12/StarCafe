using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// Mini-game "Schaltkreis": rotate conduit tiles (left click = clockwise, right click = back) so
/// the current flows from the input on the left to the generator on the right. The current is
/// shown live. Damaged tiles (red) don't conduct and spark if you point current into them.
/// Always solvable (a random path is generated first). 60 seconds.
/// </summary>
public partial class CircuitGame : CanvasLayer
{
	internal const int N = 1, E = 2, S = 4, W = 8;

	internal const int Cols = 6, Rows = 4;

	internal const float Tile = 78f;

	private const double TimeLimit = 60.0;

	internal readonly int[,] _mask = new int[Cols, Rows];

	internal readonly bool[,] _damaged = new bool[Cols, Rows];

	internal readonly bool[,] _powered = new bool[Cols, Rows];

	internal int _startRow, _endRow;

	internal bool _solved;

	private double _time = TimeLimit;

	internal readonly HashSet<Vector2I> _sparking = new HashSet<Vector2I>();

	private Action<bool> _done;

	private CircuitBoard _board;

	private Label _timer;

	private Label _status;

	private readonly RandomNumberGenerator _rng = new RandomNumberGenerator();

	public static bool IsOpen { get; private set; }

	public static void Open(Action<bool> done)
	{
		if (Engine.GetMainLoop() is not SceneTree tree || IsOpen)
		{
			return;
		}
		CircuitGame game = new CircuitGame { _done = done };
		tree.Root.AddChild(game);
	}

	public override void _Ready()
	{
		IsOpen = true;
		Layer = 32;
		ProcessMode = ProcessModeEnum.Always;
		GetTree().Paused = true;
		_rng.Randomize();
		Generate();
		Build();
		Recompute(sparks: false);
		// Automated tests only (debug builds): STARCAFE_TEST_AUTOWIN=1 wins after 1.5 s.
		if (OS.IsDebugBuild() && OS.GetEnvironment("STARCAFE_TEST_AUTOWIN") == "1")
		{
			GetTree().CreateTimer(1.5, processAlways: true).Timeout += () => Finish(true, null);
		}
	}

	public override void _Process(double delta)
	{
		if (_solved)
		{
			return;
		}
		_time -= delta;
		_timer.Text = $"{Mathf.Max(0, (int)_time)} s";
		_timer.AddThemeColorOverride("font_color", _time > 15 ? Holo.Accent : new Color(1f, 0.45f, 0.4f));
		if (_time <= 0)
		{
			PlayerCombat.Instance?.TakeDamage(12f, Vector2.Zero, "Kurzschluss");
			Finish(false, "Zeit abgelaufen – der Generator spuckt Funken.");
		}
		_board.QueueRedraw();
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event.IsActionPressed("ui_cancel"))
		{
			GetViewport().SetInputAsHandled();
			Finish(false, null);
		}
	}

	private void Generate()
	{
		for (int attempt = 0; attempt < 50; attempt++)
		{
			Array.Clear(_mask);
			Array.Clear(_damaged);
			_startRow = _rng.RandiRange(0, Rows - 1);
			List<Vector2I> path = new List<Vector2I>();
			HashSet<Vector2I> seen = new HashSet<Vector2I>();
			if (!Walk(new Vector2I(0, _startRow), path, seen) || path.Count < Cols + 1)
			{
				continue;
			}
			_endRow = path[^1].Y;
			for (int i = 0; i < path.Count; i++)
			{
				Vector2I cell = path[i];
				int m = i == 0 ? W : Dir(cell, path[i - 1]);
				m |= i == path.Count - 1 ? E : Dir(cell, path[i + 1]);
				_mask[cell.X, cell.Y] = m;
			}
			HashSet<Vector2I> onPath = new HashSet<Vector2I>(path);
			List<Vector2I> free = new List<Vector2I>();
			for (int x = 0; x < Cols; x++)
			{
				for (int y = 0; y < Rows; y++)
				{
					Vector2I c = new Vector2I(x, y);
					if (!onPath.Contains(c))
					{
						int[] shapes = { N | S, N | E, N | E | S, N | E };
						_mask[x, y] = Rotate(shapes[_rng.RandiRange(0, shapes.Length - 1)], _rng.RandiRange(0, 3));
						free.Add(c);
					}
				}
			}
			for (int i = 0; i < 3 && free.Count > 0; i++)
			{
				int pick = _rng.RandiRange(0, free.Count - 1);
				_damaged[free[pick].X, free[pick].Y] = true;
				free.RemoveAt(pick);
			}
			// Scramble.
			for (int x = 0; x < Cols; x++)
			{
				for (int y = 0; y < Rows; y++)
				{
					_mask[x, y] = Rotate(_mask[x, y], _rng.RandiRange(0, 3));
				}
			}
			Recompute(sparks: false);
			if (!_solved)
			{
				return;
			}
		}
	}

	/// <summary>Random depth-first walk from the input to any cell in the last column.</summary>
	private bool Walk(Vector2I cell, List<Vector2I> path, HashSet<Vector2I> seen)
	{
		path.Add(cell);
		seen.Add(cell);
		if (cell.X == Cols - 1 && path.Count >= Cols + 1)
		{
			return true;
		}
		List<Vector2I> next = new List<Vector2I> { cell + new Vector2I(1, 0), cell + new Vector2I(0, 1), cell + new Vector2I(0, -1), cell + new Vector2I(-1, 0) };
		// Shuffle, but prefer going right.
		for (int i = next.Count - 1; i > 0; i--)
		{
			int j = _rng.RandiRange(0, i);
			(next[i], next[j]) = (next[j], next[i]);
		}
		if (_rng.Randf() < 0.6f)
		{
			Vector2I right = cell + new Vector2I(1, 0);
			next.Remove(right);
			next.Insert(0, right);
		}
		foreach (Vector2I n in next)
		{
			if (n.X < 0 || n.Y < 0 || n.X >= Cols || n.Y >= Rows || seen.Contains(n))
			{
				continue;
			}
			if (Walk(n, path, seen))
			{
				return true;
			}
		}
		path.RemoveAt(path.Count - 1);
		return false;
	}

	private static int Dir(Vector2I from, Vector2I to)
	{
		Vector2I d = to - from;
		return d.X == 1 ? E : d.X == -1 ? W : d.Y == 1 ? S : N;
	}

	private static int Rotate(int mask, int times)
	{
		for (int i = 0; i < times; i++)
		{
			mask = ((mask << 1) | (mask >> 3)) & 15;
		}
		return mask;
	}

	private static int Opposite(int dir) => dir switch { N => S, S => N, E => W, _ => E };

	internal static Vector2I Step(int dir) => dir switch { N => new Vector2I(0, -1), S => new Vector2I(0, 1), E => new Vector2I(1, 0), _ => new Vector2I(-1, 0) };

	internal void Click(Vector2I cell, bool clockwise)
	{
		if (_solved || _damaged[cell.X, cell.Y])
		{
			return;
		}
		_mask[cell.X, cell.Y] = Rotate(_mask[cell.X, cell.Y], clockwise ? 1 : 3);
		Recompute(sparks: true);
	}

	private void Recompute(bool sparks)
	{
		Array.Clear(_powered);
		HashSet<Vector2I> nowSparking = new HashSet<Vector2I>();
		Queue<Vector2I> queue = new Queue<Vector2I>();
		Vector2I start = new Vector2I(0, _startRow);
		if ((_mask[0, _startRow] & W) != 0 && !_damaged[0, _startRow])
		{
			_powered[0, _startRow] = true;
			queue.Enqueue(start);
		}
		while (queue.Count > 0)
		{
			Vector2I c = queue.Dequeue();
			foreach (int dir in new[] { N, E, S, W })
			{
				if ((_mask[c.X, c.Y] & dir) == 0)
				{
					continue;
				}
				Vector2I n = c + Step(dir);
				if (n.X < 0 || n.Y < 0 || n.X >= Cols || n.Y >= Rows || _powered[n.X, n.Y])
				{
					continue;
				}
				if (_damaged[n.X, n.Y])
				{
					nowSparking.Add(n);
					continue;
				}
				if ((_mask[n.X, n.Y] & Opposite(dir)) != 0)
				{
					_powered[n.X, n.Y] = true;
					queue.Enqueue(n);
				}
			}
		}
		if (sparks)
		{
			foreach (Vector2I c in nowSparking)
			{
				if (!_sparking.Contains(c))
				{
					PlayerCombat.Instance?.TakeDamage(5f, Vector2.Zero, "Funkenschlag");
					_status.Text = "Funken! Beschädigte Leitung – drum herum führen.";
					_status.AddThemeColorOverride("font_color", new Color(1f, 0.5f, 0.4f));
				}
			}
		}
		_sparking.Clear();
		_sparking.UnionWith(nowSparking);
		_solved = _powered[Cols - 1, _endRow] && (_mask[Cols - 1, _endRow] & E) != 0;
		if (_solved && sparks)
		{
			_status.Text = "Strom fließt!";
			_status.AddThemeColorOverride("font_color", Holo.Accent);
			GetTree().CreateTimer(0.9, processAlways: true).Timeout += () => Finish(true, null);
		}
		_board?.QueueRedraw();
	}

	private void Finish(bool success, string message)
	{
		if (!IsOpen)
		{
			return;
		}
		IsOpen = false;
		GetTree().Paused = false;
		if (message != null)
		{
			InventoryHud.Toast(message, new Color(1f, 0.55f, 0.45f));
		}
		Action<bool> done = _done;
		QueueFree();
		done?.Invoke(success);
	}

	private void Build()
	{
		Control root = new Control { Theme = UiTheme.Get() };
		root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		AddChild(root);
		ColorRect dim = new ColorRect { Color = new Color(0f, 0.02f, 0.04f, 0.72f) };
		dim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		root.AddChild(dim);
		CenterContainer center = new CenterContainer();
		center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		root.AddChild(center);
		PanelContainer frame = new PanelContainer();
		frame.AddThemeStyleboxOverride("panel", Holo.Panel(16));
		center.AddChild(frame);
		VBoxContainer box = new VBoxContainer();
		box.AddThemeConstantOverride("separation", 8);
		frame.AddChild(box);
		HBoxContainer header = new HBoxContainer();
		box.AddChild(header);
		Label title = Holo.Caption("Generator  ·  Schaltkreis", 13);
		title.AddThemeColorOverride("font_color", Holo.Accent);
		title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		header.AddChild(title);
		_timer = Holo.Caption("", 13);
		header.AddChild(_timer);
		box.AddChild(Holo.Text_("Führe den Strom von links zum Generator. Linksklick dreht, Rechtsklick dreht zurück. Rote Felder sind beschädigt.", 13, Holo.TextDim));
		_board = new CircuitBoard { Game = this, CustomMinimumSize = new Vector2(Cols * Tile + 80f, Rows * Tile) };
		box.AddChild(_board);
		_status = Holo.Text_("", 14, Holo.Accent);
		box.AddChild(_status);
		Button cancel = Holo.Button("Abbrechen (Esc)");
		cancel.Pressed += () => Finish(false, null);
		box.AddChild(cancel);
	}
}

/// <summary>Draws and handles the tile grid.</summary>
public partial class CircuitBoard : Control
{
	public CircuitGame Game;

	private const float Left = 40f;

	public override void _GuiInput(InputEvent @event)
	{
		if (@event is InputEventMouseButton { Pressed: true } mouse && (mouse.ButtonIndex == MouseButton.Left || mouse.ButtonIndex == MouseButton.Right))
		{
			Vector2 p = mouse.Position - new Vector2(Left, 0f);
			int x = (int)Mathf.Floor(p.X / CircuitGame.Tile);
			int y = (int)Mathf.Floor(p.Y / CircuitGame.Tile);
			if (x >= 0 && y >= 0 && x < CircuitGame.Cols && y < CircuitGame.Rows)
			{
				Game.Click(new Vector2I(x, y), mouse.ButtonIndex == MouseButton.Left);
				AcceptEvent();
			}
		}
	}

	public override void _Draw()
	{
		double time = Time.GetTicksMsec() / 1000.0;
		for (int x = 0; x < CircuitGame.Cols; x++)
		{
			for (int y = 0; y < CircuitGame.Rows; y++)
			{
				Rect2 r = new Rect2(Left + x * CircuitGame.Tile + 3f, y * CircuitGame.Tile + 3f, CircuitGame.Tile - 6f, CircuitGame.Tile - 6f);
				bool damaged = Game._damaged[x, y];
				DrawRect(r, damaged ? new Color(0.22f, 0.06f, 0.06f, 0.9f) : new Color(0.05f, 0.1f, 0.14f, 0.9f));
				DrawRect(r, damaged ? new Color(1f, 0.3f, 0.25f, 0.7f) : new Color(0.35f, 0.85f, 0.95f, 0.25f), false, 1f);
				Vector2 c = r.GetCenter();
				bool powered = Game._powered[x, y];
				foreach (int dir in new[] { CircuitGame.N, CircuitGame.E, CircuitGame.S, CircuitGame.W })
				{
					if ((Game._mask[x, y] & dir) == 0)
					{
						continue;
					}
					Vector2 end = c + (Vector2)CircuitGame.Step(dir) * (CircuitGame.Tile / 2f);
					if (powered)
					{
						DrawLine(c, end, new Color(0.3f, 0.9f, 1f, 0.35f), 16f);
						DrawLine(c, end, new Color(0.75f, 1f, 1f), 6f);
					}
					else
					{
						DrawLine(c, end, damaged ? new Color(0.55f, 0.25f, 0.22f) : new Color(0.42f, 0.48f, 0.55f), 7f);
					}
				}
				DrawCircle(c, 6f, powered ? new Color(0.8f, 1f, 1f) : new Color(0.3f, 0.35f, 0.4f));
				if (damaged)
				{
					DrawLine(r.Position + new Vector2(10f, 10f), r.End - new Vector2(10f, 10f), new Color(1f, 0.35f, 0.3f), 3f);
					DrawLine(new Vector2(r.End.X - 10f, r.Position.Y + 10f), new Vector2(r.Position.X + 10f, r.End.Y - 10f), new Color(1f, 0.35f, 0.3f), 3f);
					if (Game._sparking.Contains(new Vector2I(x, y)))
					{
						float flicker = (float)(0.5 + 0.5 * Math.Sin(time * 30.0));
						DrawCircle(c, 14f * flicker + 4f, new Color(1f, 0.85f, 0.3f, 0.8f));
					}
				}
			}
		}
		// Input plug and generator socket.
		float inY = Game._startRow * CircuitGame.Tile + CircuitGame.Tile / 2f;
		DrawRect(new Rect2(4f, inY - 14f, 30f, 28f), new Color(0.3f, 0.9f, 1f, 0.9f));
		DrawLine(new Vector2(34f, inY), new Vector2(Left + 3f, inY), new Color(0.75f, 1f, 1f), 6f);
		float outY = Game._endRow * CircuitGame.Tile + CircuitGame.Tile / 2f;
		float outX = Left + CircuitGame.Cols * CircuitGame.Tile;
		Color socket = Game._solved ? new Color(0.4f, 1f, 0.6f) : new Color(1f, 0.5f, 0.3f);
		DrawLine(new Vector2(outX - 3f, outY), new Vector2(outX + 10f, outY), socket, 6f);
		DrawCircle(new Vector2(outX + 24f, outY), 14f, socket);
		DrawString(ThemeDB.FallbackFont, new Vector2(outX + 2f, outY + 34f), "GEN", HorizontalAlignment.Left, -1, 11, socket);
	}
}
