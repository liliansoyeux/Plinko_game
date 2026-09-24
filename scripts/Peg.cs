using Godot;

namespace Plinko;

public partial class Peg : StaticBody2D
{
    public float Radius = 6f;

    private static readonly Color BaseColor = new(0.78f, 0.74f, 0.9f);
    private float _flash;
    private bool _goldFlash;

    public override void _Ready()
    {
        CollisionLayer = PhysicsLayers.Board;
        CollisionMask = 0;
        AddChild(new CollisionShape2D { Shape = new CircleShape2D { Radius = Radius } });
        PhysicsMaterialOverride = new PhysicsMaterial { Bounce = 0.35f, Friction = 0.2f };
        SetProcess(false);
    }

    public void Hit(bool golden)
    {
        _flash = 1f;
        _goldFlash = golden;
        SetProcess(true);
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        _flash -= (float)delta * 2.6f;
        if (_flash <= 0f)
        {
            _flash = 0f;
            SetProcess(false);
        }
        QueueRedraw();
    }

    public override void _Draw()
    {
        var flashColor = _goldFlash ? Pal.Gold : Pal.Cyan;
        if (_flash > 0f)
        {
            Paint.Halo(this, Vector2.Zero, Radius * (2.2f + 1.6f * _flash), Pal.Alpha(flashColor, 0.7f * _flash));
        }
        DrawCircle(new Vector2(0f, Radius * 0.35f), Radius * 1.05f, new Color(0f, 0f, 0f, 0.35f));
        var body = BaseColor.Lerp(Pal.Hdr(flashColor, 2.4f), _flash);
        DrawCircle(Vector2.Zero, Radius, body);
        DrawCircle(new Vector2(-Radius * 0.3f, -Radius * 0.3f), Radius * 0.38f, Pal.Alpha(Colors.White, 0.75f));
    }
}
