using Godot;

namespace Plinko;

// Foreground: the player's legs, feet resting on the machine's ledge, toes pointing
// inward. The shoes are the character you picked.
public partial class LegsAndShoes : Node2D
{
    public CharacterDef Character = Characters.Classic;
    public float GroundY = 866f;
    public float BottomY = 1010f;

    private float _time;

    public override void _Ready()
    {
        ZIndex = 20;
    }

    public override void _Process(double delta)
    {
        _time += (float)delta;
        QueueRedraw();
    }

    public override void _Draw()
    {
        // A tiny idle tap of the right foot, so the scene feels alive.
        float tap = Mathf.Max(0f, Mathf.Sin(_time * 2.2f)) * Mathf.Max(0f, Mathf.Sin(_time * 0.37f)) * 5f;

        ShoeArt.DrawLegAndShoe(this, Character, new Vector2(52f, GroundY), 0.95f, false, BottomY, 70f);
        ShoeArt.DrawLegAndShoe(this, Character, new Vector2(848f, GroundY - tap), 0.95f, true, BottomY, 70f);
    }
}
