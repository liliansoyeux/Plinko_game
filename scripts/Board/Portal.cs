using Godot;
using System;

namespace Plinko;

// Legendary "Portail dédoubleur": every ball passing through spawns a twin. Each ball can
// only be split once per portal (twins inherit that memory), so portals can't chain forever.
public partial class Portal : Area2D
{
    public event Action<Portal, Ball> BallEntered;

    public int PortalId;
    public float Radius = 20f;

    private float _time;
    private float _pulse;
    private float _appear;

    public override void _Ready()
    {
        CollisionLayer = 0;
        CollisionMask = PhysicsLayers.Ball;
        Monitorable = false;
        AddChild(new CollisionShape2D { Shape = new CircleShape2D { Radius = Radius * 0.75f } });
        BodyEntered += body =>
        {
            if (body is Ball ball)
            {
                BallEntered?.Invoke(this, ball);
            }
        };
        _time = GD.Randf() * 10f;
        ZIndex = 2;
    }

    public void Flash() => _pulse = 1f;

    public override void _Process(double delta)
    {
        _time += (float)delta;
        _pulse = Mathf.Max(0f, _pulse - (float)delta * 2.5f);
        _appear = Mathf.Min(1f, _appear + (float)delta * 2.5f);
        QueueRedraw();
    }

    public override void _Draw()
    {
        DrawPortal(this, Vector2.Zero, Radius * (0.2f + 0.8f * _appear), _time, _pulse, 1f);
    }

    // Shared with the placement ghost so the preview looks exactly like the real thing.
    public static void DrawPortal(CanvasItem ci, Vector2 c, float r, float time, float pulse, float alpha)
    {
        var a = Pal.Prismatic(time);
        var b = Pal.Prismatic(time, 0.5f);
        Paint.Halo(ci, c, r * (1.9f + 0.4f * pulse), Pal.Alpha(a, (0.35f + 0.4f * pulse) * alpha), 5);
        ci.DrawCircle(c, r * 0.95f, new Color(0.02f, 0f, 0.05f, 0.9f * alpha));

        // Spiral arms of dots swirling inward.
        const int arms = 3;
        const int dots = 9;
        for (int arm = 0; arm < arms; arm++)
        {
            for (int i = 0; i < dots; i++)
            {
                float t = i / (float)dots;
                float angle = time * 2.6f + arm * Mathf.Tau / arms + t * 3.2f;
                float radius = r * (0.95f - 0.8f * t);
                var p = c + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                var color = (arm % 2 == 0 ? a : b).Lerp(Colors.White, t * 0.5f);
                ci.DrawCircle(p, r * (0.11f - 0.06f * t), Pal.Alpha(Pal.Hdr(color, 1.3f + pulse), alpha));
            }
        }

        ci.DrawArc(c, r, time * 1.5f, time * 1.5f + Mathf.Tau, 48, Pal.Alpha(Pal.Hdr(a, 1.4f + pulse), alpha), 2.5f, true);
        ci.DrawArc(c, r * 0.62f, -time * 2.2f, -time * 2.2f + Mathf.Pi * 1.4f, 32, Pal.Alpha(Pal.Hdr(b, 1.3f), alpha), 2f, true);
        ci.DrawCircle(c, r * 0.16f, Pal.Alpha(Pal.Hdr(Colors.White, 1.2f + pulse), alpha));
    }
}
