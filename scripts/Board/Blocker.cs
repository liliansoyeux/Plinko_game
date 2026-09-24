using Godot;

namespace Plinko;

// A deflecting bar dropped between pegs. AnimatableBody2D so boss "spinning" blockers can
// rotate while still pushing balls correctly.
public partial class Blocker : AnimatableBody2D
{
    public Vector2 Size = new(44f, 9f);
    public float SpinSpeed;
    public bool Placed; // positioned by the player (bonus) rather than a wild/malus one

    private float _flash;
    private float _appear;

    public override void _Ready()
    {
        CollisionLayer = PhysicsLayers.Board;
        CollisionMask = 0;
        SyncToPhysics = true;
        AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = Size } });
        PhysicsMaterialOverride = new PhysicsMaterial { Bounce = 0.45f, Friction = 0.15f };
    }

    public void Hit()
    {
        _flash = 1f;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (SpinSpeed != 0f)
        {
            Rotation += SpinSpeed * (float)delta;
        }
    }

    public override void _Process(double delta)
    {
        _appear = Mathf.Min(1f, _appear + (float)delta * 3f);
        if (_flash > 0f)
        {
            _flash = Mathf.Max(0f, _flash - (float)delta * 3f);
        }
        QueueRedraw();
    }

    public override void _Draw()
    {
        float t = _appear - 1f;
        float s = 1f + 2.7f * t * t * t + 1.7f * t * t;
        DrawSetTransform(Vector2.Zero, 0f, new Vector2(s, s));
        var color = SpinSpeed != 0f ? Pal.Red : Placed ? Pal.Cyan : Pal.Purple;
        var rect = new Rect2(-Size / 2f, Size);
        var glowRect = rect.Grow(4f + 4f * _flash);
        DrawColoredPolygon(Paint.RoundedRect(glowRect, glowRect.Size.Y / 2f), Pal.Alpha(color, 0.18f + 0.3f * _flash));
        DrawColoredPolygon(Paint.RoundedRect(rect, Size.Y / 2f), color.Darkened(0.35f));
        var core = new Rect2(rect.Position + new Vector2(2f, 2f), rect.Size - new Vector2(4f, 4f));
        DrawColoredPolygon(Paint.RoundedRect(core, core.Size.Y / 2f), Pal.Hdr(color, 1.3f + 1.2f * _flash));
        if (SpinSpeed != 0f)
        {
            DrawCircle(Vector2.Zero, Size.Y * 0.45f, Pal.Hdr(Colors.White, 1.5f));
        }
        DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
    }
}
