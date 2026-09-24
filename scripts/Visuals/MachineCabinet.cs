using Godot;

namespace Plinko;

// The Plinko machine: lacquered cabinet, chase-light marquee, chrome-framed screen where the
// board lives, and a front ledge (where the gauges sit and your feet rest). A neon "PLINKO"
// sign sits on top.
public partial class MachineCabinet : Node2D
{
    public static readonly Rect2 Body = new(20f, 70f, 860f, 796f);
    public static readonly Rect2 Marquee = new(40f, 84f, 820f, 88f);
    public static readonly Rect2 Screen = new(36f, 184f, 828f, 626f);
    public static readonly Rect2 Ledge = new(20f, 814f, 860f, 52f);

    public bool BossMode;

    private float _time;
    private float _flicker = 1f;
    private float _celebrate;

    public override void _Ready()
    {
        ZIndex = -20;
    }

    public void Celebrate() => _celebrate = 1f;

    public override void _Process(double delta)
    {
        _time += (float)delta;
        _celebrate = Mathf.Max(0f, _celebrate - (float)delta * 0.5f);
        // Occasional neon flicker on the sign.
        _flicker = GD.Randf() < 0.012f ? 0.35f : Mathf.Min(1f, _flicker + (float)delta * 6f);
        QueueRedraw();
    }

    public override void _Draw()
    {
        var accent = BossMode ? Pal.Red : Pal.Pink;
        var lacquerTop = BossMode ? new Color(0.3f, 0.03f, 0.05f) : new Color(0.32f, 0.05f, 0.14f);
        var lacquerBottom = BossMode ? new Color(0.1f, 0.01f, 0.02f) : new Color(0.12f, 0.02f, 0.07f);

        // Shadow on the wall.
        DrawColoredPolygon(Paint.RoundedRect(Body.Grow(14f), 30f), new Color(0, 0, 0, 0.35f));

        // Cabinet body.
        var bodyPts = Paint.RoundedRect(Body, 22f, 6);
        var bodyColors = new Color[bodyPts.Length];
        for (int i = 0; i < bodyPts.Length; i++)
        {
            float t = (bodyPts[i].Y - Body.Position.Y) / Body.Size.Y;
            bodyColors[i] = lacquerTop.Lerp(lacquerBottom, t);
        }
        DrawPolygon(bodyPts, bodyColors);
        DrawPolyline(Paint.Closed(bodyPts), Pal.Hdr(Pal.Gold, 0.9f), 3f, true);
        DrawPolyline(Paint.Closed(Paint.RoundedRect(Body.Grow(-7f), 17f, 6)), Pal.Alpha(Pal.Gold, 0.3f), 1.5f, true);

        // Side neon strips.
        foreach (float x in new[] { Body.Position.X + 12f, Body.End.X - 12f })
        {
            DrawLine(new Vector2(x, Screen.Position.Y), new Vector2(x, Screen.End.Y), Pal.Alpha(accent, 0.25f), 7f);
            DrawLine(new Vector2(x, Screen.Position.Y), new Vector2(x, Screen.End.Y), Pal.Hdr(accent, 1.6f), 2f);
        }

        DrawMarquee(accent);
        DrawScreenFrame();
        DrawLedge();
        DrawSign(accent);
    }

    private void DrawMarquee(Color accent)
    {
        var m = Marquee;
        DrawColoredPolygon(Paint.RoundedRect(m, 14f), new Color(0.04f, 0.015f, 0.06f));
        DrawPolyline(Paint.Closed(Paint.RoundedRect(m, 14f)), Pal.Alpha(Pal.Gold, 0.6f), 2f, true);

        // Chase lights around the marquee. Faster, all-on blinking while celebrating.
        float perimeter = 2f * (m.Size.X + m.Size.Y);
        const float spacing = 24f;
        int count = (int)(perimeter / spacing);
        float speed = _celebrate > 0f ? 22f : 7f;
        int phase = (int)(_time * speed);
        for (int i = 0; i < count; i++)
        {
            var p = PointOnRect(m.Grow(-7f), i * spacing);
            bool lit = _celebrate > 0f ? ((int)(_time * 10f) % 2 == 0) : ((i + phase) % 3 == 0);
            var bulb = lit ? Pal.Hdr(Pal.Gold, 2.2f) : new Color(0.35f, 0.24f, 0.12f);
            if (lit)
            {
                DrawCircle(p, 6f, Pal.Alpha(Pal.Gold, 0.18f));
            }
            DrawCircle(p, 2.6f, bulb);
        }
    }

    private void DrawScreenFrame()
    {
        var s = Screen;
        DrawColoredPolygon(Paint.RoundedRect(s.Grow(6f), 18f), new Color(0.55f, 0.5f, 0.6f));
        DrawColoredPolygon(Paint.RoundedRect(s.Grow(4f), 16f), new Color(0.2f, 0.16f, 0.24f));
        DrawColoredPolygon(Paint.RoundedRect(s, 14f), new Color(0.02f, 0.008f, 0.035f));
        // Inner top shadow for depth.
        Paint.VerticalGradient(this, new Rect2(s.Position + new Vector2(8f, 2f), new Vector2(s.Size.X - 16f, 30f)),
            new Color(0, 0, 0, 0.5f), new Color(0, 0, 0, 0f));
        // Glass reflection.
        DrawColoredPolygon(new[]
        {
            s.Position + new Vector2(s.Size.X * 0.62f, 4f), s.Position + new Vector2(s.Size.X * 0.74f, 4f),
            s.Position + new Vector2(s.Size.X * 0.54f, s.Size.Y * 0.45f), s.Position + new Vector2(s.Size.X * 0.42f, s.Size.Y * 0.45f),
        }, new Color(1f, 1f, 1f, 0.018f));
    }

    private void DrawLedge()
    {
        var l = Ledge;
        Paint.VerticalGradient(this, l, new Color(0.3f, 0.26f, 0.34f), new Color(0.1f, 0.08f, 0.12f));
        DrawLine(l.Position, new Vector2(l.End.X, l.Position.Y), Pal.Hdr(new Color(0.9f, 0.85f, 1f), 1.1f), 2f);
        DrawLine(new Vector2(l.Position.X, l.End.Y), l.End, new Color(0, 0, 0, 0.6f), 3f);
        // Rivets.
        for (float x = l.Position.X + 24f; x < l.End.X - 10f; x += 64f)
        {
            DrawCircle(new Vector2(x, l.Position.Y + 10f), 2f, new Color(0.7f, 0.65f, 0.75f));
        }
    }

    private void DrawSign(Color accent)
    {
        var font = Fonts.Black;
        var center = new Vector2(450f, 38f);
        const int size = 48;
        float glow = _flicker * (1f + 0.4f * _celebrate);

        // Sign backplate on little posts.
        DrawLine(new Vector2(380f, 58f), new Vector2(380f, 72f), new Color(0.4f, 0.35f, 0.45f), 4f);
        DrawLine(new Vector2(520f, 58f), new Vector2(520f, 72f), new Color(0.4f, 0.35f, 0.45f), 4f);
        var plate = new Rect2(330f, 8f, 240f, 56f);
        DrawColoredPolygon(Paint.RoundedRect(plate, 12f), new Color(0.05f, 0.02f, 0.07f, 0.95f));
        DrawPolyline(Paint.Closed(Paint.RoundedRect(plate, 12f)), Pal.Hdr(Pal.Cyan, 1.4f * glow), 2f, true);

        Paint.Halo(this, center, 110f, Pal.Alpha(accent, 0.12f * glow), 5);
        Paint.TextCentered(this, font, center, "PLINKO", size, Pal.Hdr(accent.Lightened(0.45f), 1.15f + 0.25f * glow), 4, Pal.Hdr(accent, 0.9f * glow));
    }

    private static Vector2 PointOnRect(Rect2 r, float distance)
    {
        float w = r.Size.X;
        float h = r.Size.Y;
        distance = Mathf.PosMod(distance, 2f * (w + h));
        if (distance < w) return r.Position + new Vector2(distance, 0f);
        distance -= w;
        if (distance < h) return r.Position + new Vector2(w, distance);
        distance -= h;
        if (distance < w) return r.Position + new Vector2(w - distance, h);
        distance -= w;
        return r.Position + new Vector2(0f, h - distance);
    }
}
