using Godot;

[GlobalClass]
public partial class NpcPersona : Resource
{
    [Export] public string DisplayName { get; set; } = "NPC";

    [Export(PropertyHint.MultilineText)]
    public string SystemPrompt { get; set; } = "";

    [Export] public Texture2D Portrait { get; set; }
}
