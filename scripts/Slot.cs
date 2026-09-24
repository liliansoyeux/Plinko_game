using Godot;
using System;

namespace Plinko;

public partial class Slot : Area2D
{
    public event Action<Slot, Ball> BallEntered;

    public float Multiplier = 1f;
    public Vector2 SlotSize = new(46f, 60f);

    private float _pulse;
    private double _blinkRemaining;
    private float _idlePhase;

    public override void _Ready()
    {
        CollisionLayer = 0;
        CollisionMask = PhysicsLayers.Ball;
        Monitorable = false;
        AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = SlotSize } });
        BodyEntered += OnBodyEntered;
        _idlePhase = GD.Randf() * Mathf.Tau;
    }

    public void SetMultiplier(float value, bool celebrate)
    {
        Multiplier = value;
        if (celebrate)
        {
            _blinkRemaining = 2.5;
            _pulse = 1f;
        }
        QueueRedraw();
    }

    public void Pulse()
    {
        _pulse = 1f;
    }

    private void OnBodyEntered(Node2D body)
    {
        if (body is Ball ball)
        {
            BallEntered?.Invoke(this, ball);
        }
    }

    public override void _Process(double delta)
    {
        _pulse = Mathf.Max(0f, _pulse - (float)delta * 2.8f);
        _blinkRemaining = Math.Max(0.0, _blinkRemaining - delta);
        _idlePhase += (float)delta * (Multiplier >= 10f ? 3f : 1.2f);
        QueueRedraw();
    }

    public override void _Draw()
    {
        var color = Pal.ForMultiplier(Multiplier);
        bool blinking = _blinkRemaining > 0 && ((int)(_blinkRemaining * 8) % 2 == 0);
        float idle = Multiplier >= 10f ? 0.5f + 0.5f * Mathf.Sin(_idlePhase) : 0.3f;
        float energy = Mathf.Max(_pulse, blinking ? 1f : 0f);

        float w = SlotSize.X;
        float h = SlotSize.Y;
        float grow = 1f + 0.12f * _pulse;
        DrawSetTransform(new Vector2(0f, -h * 0.06f * _pulse), 0f, new Vector2(grow, grow));

        var rect = new Rect2(-w / 2f + 1.5f, -h / 2f, w - 3f, h);
        float radius = Mathf.Min(8f, w * 0.22f);

        // Glow behind the cup, stronger for jackpots and on hit.
        var glowRect = rect.Grow(3f + 5f * energy);
        DrawColoredPolygon(Paint.RoundedRect(glowRect, radius + 3f), Pal.Alpha(color, 0.08f + 0.12f * idle + 0.35f * energy));

        var outline = Paint.RoundedRect(rect, radius);
        DrawPolygon(outline, GradientColors(outline, rect, color.Darkened(0.55f), color.Darkened(0.82f)));
        DrawPolyline(Paint.Closed(outline), Pal.Hdr(color, 1f + 0.9f * idle + 1.8f * energy), 2f, true);

        // Neon lip along the top edge, where balls come in.
        DrawLine(new Vector2(rect.Position.X + 3f, rect.Position.Y + 1.5f), new Vector2(rect.End.X - 3f, rect.Position.Y + 1.5f),
            Pal.Hdr(color, 1.6f + 2f * energy), 3f, true);

        string text = Pal.FormatMultiplier(Multiplier);
        int size = (int)Mathf.Clamp(w * 0.4f, 11f, 19f);
        var font = Fonts.Bold;
        while (size > 9 && font.GetStringSize(text, HorizontalAlignment.Left, -1, size).X > w - 6f)
        {
            size--;
        }
        var textColor = energy > 0.2f ? Pal.Hdr(Colors.White, 1.4f) : color.Lightened(0.35f);
        Paint.TextCentered(this, font, new Vector2(0f, 2f), text, size, textColor, 3);

        DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
    }

    private static Color[] GradientColors(Vector2[] points, Rect2 rect, Color top, Color bottom)
    {
        var colors = new Color[points.Length];
        for (int i = 0; i < points.Length; i++)
        {
            float t = (points[i].Y - rect.Position.Y) / rect.Size.Y;
            colors[i] = top.Lerp(bottom, t);
        }
        return colors;
    }
}
