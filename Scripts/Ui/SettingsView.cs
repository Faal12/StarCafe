using Godot;

public partial class SettingsView : VBoxContainer
{
	private const string NotActiveYet = "Noch ohne Wirkung – folgt mit dem entsprechenden System.";

	public override void _Ready()
	{
		SizeFlagsHorizontal = SizeFlags.ExpandFill;
		AddThemeConstantOverride("separation", 10);
		Build();
	}

	private void Build()
	{
		UiKit.ClearChildren(this);

		AddChild(UiKit.Header("Humor & Sprache"));
		AddChild(UiKit.Row("Kraftausdrücke",
			UiKit.StateToggle("erlaubt", "verboten", GameSettings.Profanity, on => GameSettings.Profanity = on),
			"Gilt für alle KI-Figuren."));
		AddChild(UiKit.Row("Humor-Härte",
			UiKit.EnumOptions(GameSettings.Humor, value => GameSettings.Humor = value),
			"Mild: neckend. Hart: bissig, schwarzer Humor – aber nur, wenn du das Thema selbst dorthin lenkst."));

		AddChild(UiKit.Header("Floki"));
		AddChild(UiKit.Row("Kommentare (Häufigkeit)",
			UiKit.EnumOptions(GameSettings.FlokiComments, value => GameSettings.FlokiComments = value),
			NotActiveYet));

		AddChild(UiKit.Header("Inhalt"));
		AddChild(UiKit.Row("Gewaltdarstellung",
			UiKit.EnumOptions(GameSettings.Violence, value => GameSettings.Violence = value),
			"Angedeutet: ohne Details. Normal: konkreter, aber ohne drastische Schilderung."));
		AddChild(UiKit.Row("Ereignisse (Häufigkeit)",
			UiKit.EnumOptions(GameSettings.Conflicts, value => GameSettings.Conflicts = value),
			NotActiveYet));
		AddChild(UiKit.Row("Radio-Nachrichten",
			UiKit.StateToggle("an", "aus", GameSettings.RadioEnabled, on => GameSettings.RadioEnabled = on),
			NotActiveYet));

		AddChild(UiKit.Header("Spielgefühl"));
		AddChild(UiKit.Row("Textgeschwindigkeit",
			UiKit.EnumOptions(GameSettings.TextSpeed, value => GameSettings.TextSpeed = value),
			"Wie schnell Antworten im Dialog erscheinen."));

		AddChild(UiKit.Header("Anzeige & Ton"));
		AddChild(UiKit.SliderRow("Gesamtlautstärke", 0, 100, 5, GameSettings.MasterVolume * 100,
			value => $"{value:0} %",
			value => GameSettings.MasterVolume = value / 100.0,
			"Wirkt auf die Sprachausgabe und künftige Spielgeräusche."));
		AddChild(UiKit.Row("Vollbild",
			UiKit.StateToggle("an", "aus", GameSettings.Fullscreen, on => GameSettings.Fullscreen = on)));
		AddChild(UiKit.Row("Dialog-Schriftgröße",
			UiKit.EnumOptions(GameSettings.DialogueTextSize, value => GameSettings.DialogueTextSize = value)));

		var reset = new Button { Text = "Auf Standard zurücksetzen", SizeFlagsHorizontal = SizeFlags.ShrinkBegin };
		reset.Pressed += () =>
		{
			GameSettings.ResetToDefaults();
			Build();
		};
		AddChild(reset);
	}
}
