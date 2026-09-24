using Godot;

namespace Plinko;

// Side-profile shoes (toe pointing right at scale +1) for each ShoeStyle. Shared by the
// in-game legs and the character-select cards so both always show the same pair.
public static class ShoeArt
{
    private static readonly Color Skin = new(0.93f, 0.72f, 0.58f);

    private struct Pen
    {
        public CanvasItem Ci;
        public Vector2 Origin;
        public float Scale;
        public bool Mirrored;

        public Vector2 T(Vector2 p) => Origin + new Vector2(Mirrored ? -p.X : p.X, p.Y) * Scale;

        public Vector2[] T(Vector2[] pts)
        {
            var result = new Vector2[pts.Length];
            for (int i = 0; i < pts.Length; i++)
            {
                result[i] = T(pts[i]);
            }
            return result;
        }

        public void Fill(Color color, params Vector2[] pts) => Ci.DrawColoredPolygon(T(pts), color);

        public void Line(Color color, float width, params Vector2[] pts)
        {
            Ci.DrawPolyline(T(pts), color, width * Scale, true);
        }

        public void Dot(Vector2 p, float r, Color color) => Ci.DrawCircle(T(p), r * Scale, color);
    }

    // Where the leg meets the shoe, in shoe space: (back x, front x, y).
    public static (float back, float front, float y) Collar(ShoeStyle style) => style switch
    {
        ShoeStyle.Boot => (-2f, 44f, -104f),
        ShoeStyle.Heel => (0f, 40f, -70f),
        ShoeStyle.Sneaker => (2f, 40f, -58f),
        ShoeStyle.FlipFlop => (4f, 38f, -58f),
        _ => (2f, 36f, -50f),
    };

    public static void DrawLegAndShoe(CanvasItem ci, CharacterDef def, Vector2 heelGround, float scale, bool mirrored, float legBottomY, float legSpread)
    {
        var pen = new Pen { Ci = ci, Origin = heelGround, Scale = scale, Mirrored = mirrored };
        var (back, front, collarY) = Collar(def.Style);

        // The leg comes up from the bottom of the screen and disappears behind the shoe, so
        // it's drawn first; only the cuff hem is drawn over the collar afterwards.
        float cuffY = def.Style == ShoeStyle.FlipFlop ? collarY - 18f : collarY - 2f;
        float bottomLocalY = (legBottomY - heelGround.Y) / scale;
        float outward = -legSpread / scale;
        var topBack = new Vector2(back - 6f, cuffY);
        var topFront = new Vector2(front + 6f, cuffY);
        var bottomFront = new Vector2(front + 56f + outward, bottomLocalY);
        var bottomBack = new Vector2(back - 76f + outward, bottomLocalY);

        var lit = def.PantsColor.Lightened(0.22f);
        var shade = def.PantsColor.Darkened(0.45f);
        ci.DrawPolygon(pen.T(new[] { topBack, topFront, bottomFront, bottomBack }), new[] { lit, lit, shade, shade });
        // Shadowed outer side and a crease down the shin.
        pen.Fill(Pal.Alpha(Colors.Black, 0.28f), topBack, topBack + new Vector2(12f, 0f), bottomBack + new Vector2(40f, 0f), bottomBack);
        pen.Line(Pal.Alpha(def.PantsColor.Lightened(0.35f), 0.6f), 2f,
            new Vector2((back + front) / 2f - 2f, cuffY + 14f), new Vector2((back + front) / 2f - 16f + outward, bottomLocalY));
        // Neon rim light from the machine on the inner edge.
        pen.Line(Pal.Alpha(Pal.Pink, 0.75f), 3f, topFront + new Vector2(0f, 6f), bottomFront);

        if (def.Style == ShoeStyle.FlipFlop)
        {
            pen.Fill(Skin.Darkened(0.05f), new Vector2(back + 2f, cuffY), new Vector2(front - 2f, cuffY), new Vector2(front, collarY + 10f), new Vector2(back + 4f, collarY + 10f));
        }

        DrawShoe(pen, def);

        // Cuff hem resting on the collar.
        pen.Fill(def.PantsColor.Darkened(0.15f), new Vector2(back - 8f, cuffY - 6f), new Vector2(front + 8f, cuffY - 6f),
            new Vector2(front + 9f, cuffY + 5f), new Vector2(back - 9f, cuffY + 5f));
        pen.Line(Pal.Alpha(def.PantsColor.Lightened(0.3f), 0.7f), 1.5f, new Vector2(back - 8f, cuffY - 6f), new Vector2(front + 8f, cuffY - 6f));
    }

    public static void DrawShoeOnly(CanvasItem ci, CharacterDef def, Vector2 heelGround, float scale, bool mirrored)
    {
        DrawShoe(new Pen { Ci = ci, Origin = heelGround, Scale = scale, Mirrored = mirrored }, def);
    }

    private static void DrawShoe(Pen p, CharacterDef def)
    {
        // Ground shadow.
        p.Fill(new Color(0, 0, 0, 0.35f), new Vector2(-10f, -2f), new Vector2(170f, -2f), new Vector2(160f, 5f), new Vector2(-4f, 5f));

        switch (def.Style)
        {
            case ShoeStyle.Sneaker: Sneaker(p, def); break;
            case ShoeStyle.Heel: Heel(p, def); break;
            case ShoeStyle.Boot: Boot(p, def); break;
            case ShoeStyle.FlipFlop: FlipFlop(p, def); break;
            default: Derby(p, def); break;
        }
    }

    private static void Derby(Pen p, CharacterDef d)
    {
        var c = d.ShoeColor;
        p.Fill(new Color(0.06f, 0.04f, 0.04f),
            new Vector2(-3f, -9f), new Vector2(156f, -9f), new Vector2(160f, -4f), new Vector2(152f, 0f), new Vector2(0f, 0f), new Vector2(-4f, -4f));
        p.Fill(c,
            new Vector2(0f, -9f), new Vector2(-3f, -30f), new Vector2(2f, -50f), new Vector2(34f, -52f), new Vector2(62f, -42f),
            new Vector2(104f, -32f), new Vector2(138f, -26f), new Vector2(154f, -18f), new Vector2(158f, -9f));
        // Toe cap seam + heel counter.
        p.Line(d.AccentColor, 2f, new Vector2(112f, -31f), new Vector2(118f, -20f), new Vector2(122f, -9f));
        p.Fill(c.Darkened(0.25f), new Vector2(0f, -9f), new Vector2(-2f, -30f), new Vector2(24f, -28f), new Vector2(30f, -9f));
        // Collar opening.
        p.Fill(new Color(0.05f, 0.03f, 0.03f), new Vector2(4f, -50f), new Vector2(33f, -52f), new Vector2(30f, -46f), new Vector2(6f, -45f));
        // Laces.
        for (int i = 0; i < 3; i++)
        {
            float x = 44f + i * 12f;
            p.Line(d.AccentColor.Lightened(0.3f), 2f, new Vector2(x, -47f + i * 2.5f), new Vector2(x + 10f, -41f + i * 2.5f));
        }
        // Shine.
        p.Line(new Color(1f, 1f, 1f, 0.35f), 3f, new Vector2(120f, -27f), new Vector2(140f, -23f), new Vector2(150f, -16f));
    }

    private static void Sneaker(Pen p, CharacterDef d)
    {
        var c = d.ShoeColor;
        var white = d.AccentColor;
        p.Fill(c,
            new Vector2(0f, -14f), new Vector2(-3f, -40f), new Vector2(4f, -60f), new Vector2(36f, -62f), new Vector2(60f, -48f),
            new Vector2(102f, -38f), new Vector2(140f, -31f), new Vector2(160f, -22f), new Vector2(163f, -14f));
        // Toe cap.
        p.Fill(c.Lightened(0.25f), new Vector2(118f, -35f), new Vector2(140f, -31f), new Vector2(160f, -22f), new Vector2(163f, -14f), new Vector2(116f, -14f));
        // Swoosh.
        p.Fill(white, new Vector2(24f, -22f), new Vector2(60f, -30f), new Vector2(110f, -44f), new Vector2(66f, -24f), new Vector2(30f, -16f));
        // Sole.
        p.Fill(white,
            new Vector2(-5f, -15f), new Vector2(164f, -15f), new Vector2(168f, -7f), new Vector2(158f, 0f), new Vector2(3f, 0f), new Vector2(-5f, -6f));
        p.Line(c.Darkened(0.1f), 2.5f, new Vector2(-2f, -7f), new Vector2(164f, -7f));
        // Heel tab + collar.
        p.Fill(white, new Vector2(-4f, -46f), new Vector2(5f, -62f), new Vector2(12f, -60f), new Vector2(4f, -44f));
        p.Fill(new Color(0.08f, 0.05f, 0.08f), new Vector2(6f, -60f), new Vector2(35f, -62f), new Vector2(32f, -55f), new Vector2(8f, -54f));
        // Laces.
        for (int i = 0; i < 4; i++)
        {
            float x = 44f + i * 12f;
            p.Line(white, 2.5f, new Vector2(x, -56f + i * 3.5f), new Vector2(x + 10f, -49f + i * 3.5f));
        }
    }

    private static void Heel(Pen p, CharacterDef d)
    {
        var c = d.ShoeColor;
        // Stiletto spike.
        p.Fill(c.Darkened(0.35f), new Vector2(2f, -50f), new Vector2(12f, -50f), new Vector2(10f, 0f), new Vector2(6f, 0f));
        // Red lacquered sole.
        p.Fill(d.AccentColor, new Vector2(0f, -50f), new Vector2(40f, -34f), new Vector2(96f, -3f), new Vector2(152f, -3f), new Vector2(154f, 0f),
            new Vector2(96f, 1f), new Vector2(36f, -30f), new Vector2(-1f, -46f));
        // Upper (pump).
        p.Fill(c, new Vector2(-1f, -50f), new Vector2(0f, -70f), new Vector2(14f, -68f), new Vector2(40f, -46f), new Vector2(96f, -24f),
            new Vector2(140f, -16f), new Vector2(156f, -8f), new Vector2(152f, -3f), new Vector2(96f, -3f), new Vector2(40f, -34f));
        // Ankle opening showing skin.
        p.Fill(Skin, new Vector2(14f, -68f), new Vector2(40f, -46f), new Vector2(96f, -24f), new Vector2(60f, -44f), new Vector2(34f, -62f));
        p.Line(Pal.Hdr(new Color(1f, 0.95f, 0.7f), 1.3f), 3f, new Vector2(100f, -21f), new Vector2(140f, -14f));
        p.Dot(new Vector2(22f, -60f), 3f, Pal.Hdr(new Color(1f, 0.95f, 0.7f), 1.4f));
    }

    private static void Boot(Pen p, CharacterDef d)
    {
        var c = d.ShoeColor;
        // Cuban heel block.
        p.Fill(c.Darkened(0.5f), new Vector2(0f, -10f), new Vector2(30f, -10f), new Vector2(26f, 0f), new Vector2(4f, 0f));
        p.Fill(c.Darkened(0.5f), new Vector2(26f, -10f), new Vector2(170f, -10f), new Vector2(162f, -5f), new Vector2(40f, -5f));
        // Shaft + foot.
        p.Fill(c, new Vector2(0f, -10f), new Vector2(-6f, -112f), new Vector2(46f, -114f), new Vector2(50f, -62f), new Vector2(82f, -42f),
            new Vector2(132f, -30f), new Vector2(162f, -18f), new Vector2(172f, -10f));
        // Stitching patterns.
        p.Line(d.AccentColor, 2f, new Vector2(4f, -100f), new Vector2(20f, -84f), new Vector2(38f, -100f));
        p.Line(d.AccentColor, 2f, new Vector2(6f, -80f), new Vector2(20f, -64f), new Vector2(40f, -80f));
        p.Line(d.AccentColor, 2f, new Vector2(90f, -38f), new Vector2(116f, -30f), new Vector2(146f, -22f));
        // Pull tab and top trim.
        p.Fill(c.Darkened(0.3f), new Vector2(-6f, -112f), new Vector2(46f, -114f), new Vector2(45f, -106f), new Vector2(-5f, -104f));
        p.Line(new Color(1f, 1f, 1f, 0.25f), 3f, new Vector2(130f, -28f), new Vector2(160f, -17f));
    }

    private static void FlipFlop(Pen p, CharacterDef d)
    {
        // Sole: two layers.
        p.Fill(d.AccentColor, new Vector2(-3f, -5f), new Vector2(154f, -5f), new Vector2(158f, -2f), new Vector2(152f, 0f), new Vector2(0f, 0f));
        p.Fill(d.ShoeColor, new Vector2(-3f, -10f), new Vector2(152f, -10f), new Vector2(158f, -5f), new Vector2(-3f, -5f));
        // Bare foot.
        p.Fill(Skin, new Vector2(2f, -10f), new Vector2(2f, -40f), new Vector2(8f, -58f), new Vector2(38f, -58f), new Vector2(46f, -40f),
            new Vector2(80f, -30f), new Vector2(126f, -22f), new Vector2(146f, -18f), new Vector2(152f, -10f));
        for (int i = 0; i < 4; i++)
        {
            p.Dot(new Vector2(128f + i * 7f, -14f - (i == 3 ? 3f : 0f)), 4.2f, Skin.Darkened(0.08f));
        }
        p.Dot(new Vector2(150f, -15f), 2.5f, Pal.Pink);
        // Strap.
        p.Line(d.ShoeColor.Darkened(0.1f), 6f, new Vector2(66f, -30f), new Vector2(108f, -24f), new Vector2(128f, -12f));
        p.Dot(new Vector2(128f, -12f), 3.5f, d.AccentColor);
    }
}
