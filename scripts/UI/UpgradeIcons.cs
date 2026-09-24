using Godot;

namespace Plinko;

public enum UpgradeIcon
{
    Rows, SmallBall, BigBall, Multiplier, MultiplierDown, TwinBall, ExtraBalls, FewerBalls, Blocker,
    Shield, ShieldBroken, GoldBall, Peg, Xp, Chest, Star, Cocktail, NarrowSlots, Skull, Portal,
    Reroll, Cards, Heart, Gear, Clock,
}

// Simple vector pictograms for upgrade cards — no font glyph or texture dependency.
public static class UpgradeIcons
{
    public static void Draw(CanvasItem ci, Vector2 c, float r, UpgradeIcon icon, Color color)
    {
        var light = color.Lightened(0.35f);
        var white = new Color(0.98f, 0.95f, 1f);
        var dark = new Color(0.06f, 0.03f, 0.09f);

        switch (icon)
        {
            case UpgradeIcon.Rows:
                for (int row = 0; row < 4; row++)
                {
                    for (int i = 0; i <= row; i++)
                    {
                        var p = c + new Vector2((i - row / 2f) * r * 0.42f, (row - 1.5f) * r * 0.4f);
                        ci.DrawCircle(p, r * 0.1f, row == 3 ? light : white);
                    }
                }
                Plus(ci, c + new Vector2(r * 0.62f, -r * 0.55f), r * 0.22f, light);
                break;

            case UpgradeIcon.SmallBall:
                ci.DrawArc(c, r * 0.62f, 0f, Mathf.Tau, 32, Pal.Alpha(white, 0.35f), 2f, true);
                ci.DrawCircle(c, r * 0.32f, white);
                Arrow(ci, c + new Vector2(r * 0.58f, -r * 0.58f), c + new Vector2(r * 0.3f, -r * 0.3f), light);
                break;

            case UpgradeIcon.BigBall:
                ci.DrawCircle(c, r * 0.6f, white);
                ci.DrawArc(c, r * 0.32f, 0f, Mathf.Tau, 32, Pal.Alpha(dark, 0.5f), 2f, true);
                Arrow(ci, c + new Vector2(r * 0.45f, -r * 0.45f), c + new Vector2(r * 0.8f, -r * 0.8f), light);
                break;

            case UpgradeIcon.Multiplier:
            case UpgradeIcon.MultiplierDown:
                Cross(ci, c + new Vector2(-r * 0.15f, 0f), r * 0.38f, white, r * 0.14f);
                if (icon == UpgradeIcon.Multiplier)
                {
                    Arrow(ci, c + new Vector2(r * 0.55f, r * 0.35f), c + new Vector2(r * 0.55f, -r * 0.45f), light);
                }
                else
                {
                    Arrow(ci, c + new Vector2(r * 0.55f, -r * 0.45f), c + new Vector2(r * 0.55f, r * 0.35f), light);
                }
                break;

            case UpgradeIcon.TwinBall:
                ci.DrawCircle(c + new Vector2(-r * 0.25f, 0f), r * 0.3f, white);
                ci.DrawCircle(c + new Vector2(r * 0.28f, -r * 0.1f), r * 0.3f, light);
                break;

            case UpgradeIcon.ExtraBalls:
            case UpgradeIcon.FewerBalls:
                for (int i = 0; i < 3; i++)
                {
                    ci.DrawCircle(c + new Vector2((i - 1) * r * 0.42f, r * 0.2f), r * 0.17f, white);
                }
                if (icon == UpgradeIcon.ExtraBalls)
                {
                    Plus(ci, c + new Vector2(0f, -r * 0.4f), r * 0.24f, light);
                }
                else
                {
                    ci.DrawLine(c + new Vector2(-r * 0.24f, -r * 0.4f), c + new Vector2(r * 0.24f, -r * 0.4f), light, r * 0.12f);
                }
                break;

            case UpgradeIcon.Blocker:
                ci.DrawSetTransform(c, -0.4f, Vector2.One);
                ci.DrawColoredPolygon(Paint.RoundedRect(new Rect2(-r * 0.6f, -r * 0.12f, r * 1.2f, r * 0.24f), r * 0.12f), white);
                ci.DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
                ci.DrawCircle(c + new Vector2(-r * 0.2f, -r * 0.55f), r * 0.14f, light);
                break;

            case UpgradeIcon.Shield:
            case UpgradeIcon.ShieldBroken:
            {
                var pts = new[]
                {
                    c + new Vector2(0f, -r * 0.62f), c + new Vector2(r * 0.5f, -r * 0.4f), c + new Vector2(r * 0.42f, r * 0.2f),
                    c + new Vector2(0f, r * 0.62f), c + new Vector2(-r * 0.42f, r * 0.2f), c + new Vector2(-r * 0.5f, -r * 0.4f),
                };
                ci.DrawColoredPolygon(pts, white);
                if (icon == UpgradeIcon.ShieldBroken)
                {
                    ci.DrawPolyline(new[] { c + new Vector2(-r * 0.05f, -r * 0.62f), c + new Vector2(r * 0.12f, -r * 0.15f), c + new Vector2(-r * 0.1f, r * 0.1f), c + new Vector2(r * 0.05f, r * 0.62f) }, dark, r * 0.1f);
                }
                else
                {
                    ci.DrawPolyline(new[] { c + new Vector2(-r * 0.2f, 0f), c + new Vector2(-r * 0.04f, r * 0.18f), c + new Vector2(r * 0.24f, -r * 0.2f) }, color.Darkened(0.3f), r * 0.1f);
                }
                break;
            }

            case UpgradeIcon.GoldBall:
                ci.DrawCircle(c, r * 0.45f, Pal.Gold);
                ci.DrawCircle(c + new Vector2(-r * 0.14f, -r * 0.16f), r * 0.14f, white);
                for (int i = 0; i < 4; i++)
                {
                    float a = i * Mathf.Pi / 2f + 0.4f;
                    var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    ci.DrawLine(c + d * r * 0.58f, c + d * r * 0.78f, light, r * 0.08f);
                }
                break;

            case UpgradeIcon.Peg:
                for (int i = 0; i < 3; i++)
                {
                    ci.DrawCircle(c + new Vector2((i - 1) * r * 0.5f, r * 0.25f), r * 0.14f, white);
                }
                ci.DrawCircle(c + new Vector2(0f, -r * 0.3f), r * 0.2f, light);
                Plus(ci, c + new Vector2(r * 0.5f, -r * 0.4f), r * 0.18f, light);
                break;

            case UpgradeIcon.Xp:
            {
                var star = StarPoints(c, r * 0.6f, r * 0.26f);
                ci.DrawColoredPolygon(star, white);
                Arrow(ci, c + new Vector2(r * 0.62f, r * 0.4f), c + new Vector2(r * 0.62f, -r * 0.3f), light);
                break;
            }

            case UpgradeIcon.Chest:
                ci.DrawRect(new Rect2(c + new Vector2(-r * 0.5f, -r * 0.1f), new Vector2(r, r * 0.5f)), white);
                ci.DrawColoredPolygon(Paint.RoundedRect(new Rect2(c + new Vector2(-r * 0.5f, -r * 0.45f), new Vector2(r, r * 0.34f)), r * 0.14f), light);
                ci.DrawRect(new Rect2(c + new Vector2(-r * 0.08f, -r * 0.18f), new Vector2(r * 0.16f, r * 0.2f)), dark);
                break;

            case UpgradeIcon.Star:
                ci.DrawColoredPolygon(StarPoints(c, r * 0.66f, r * 0.28f), white);
                ci.DrawColoredPolygon(StarPoints(c, r * 0.36f, r * 0.15f), light);
                break;

            case UpgradeIcon.Cocktail:
                ci.DrawColoredPolygon(new[] { c + new Vector2(-r * 0.5f, -r * 0.45f), c + new Vector2(r * 0.5f, -r * 0.45f), c + new Vector2(0f, r * 0.1f) }, white);
                ci.DrawLine(c + new Vector2(0f, r * 0.1f), c + new Vector2(0f, r * 0.5f), white, r * 0.08f);
                ci.DrawLine(c + new Vector2(-r * 0.25f, r * 0.52f), c + new Vector2(r * 0.25f, r * 0.52f), white, r * 0.08f);
                ci.DrawCircle(c + new Vector2(r * 0.3f, -r * 0.5f), r * 0.14f, light);
                break;

            case UpgradeIcon.NarrowSlots:
                for (int i = 0; i < 3; i++)
                {
                    float x = (i - 1) * r * 0.45f;
                    ci.DrawRect(new Rect2(c + new Vector2(x - r * 0.12f, -r * 0.2f), new Vector2(r * 0.24f, r * 0.55f)), white, false, 2f);
                }
                Arrow(ci, c + new Vector2(-r * 0.75f, -r * 0.45f), c + new Vector2(-r * 0.35f, -r * 0.45f), light);
                Arrow(ci, c + new Vector2(r * 0.75f, -r * 0.45f), c + new Vector2(r * 0.35f, -r * 0.45f), light);
                break;

            case UpgradeIcon.Portal:
                ci.DrawArc(c, r * 0.62f, 0f, Mathf.Tau, 40, light, r * 0.1f, true);
                ci.DrawArc(c, r * 0.4f, 0f, Mathf.Tau, 32, white, r * 0.08f, true);
                ci.DrawCircle(c, r * 0.18f, dark);
                ci.DrawCircle(c + new Vector2(-r * 0.3f, r * 0.72f), r * 0.13f, white);
                ci.DrawCircle(c + new Vector2(r * 0.3f, r * 0.72f), r * 0.13f, white);
                break;

            case UpgradeIcon.Reroll:
                ci.DrawArc(c, r * 0.48f, -0.3f, Mathf.Pi * 1.55f, 32, white, r * 0.12f, true);
                Arrow(ci, c + new Vector2(r * 0.46f, r * 0.05f), c + new Vector2(r * 0.46f, -r * 0.35f), light);
                ci.DrawCircle(c, r * 0.14f, light);
                break;

            case UpgradeIcon.Cards:
                for (int i = 0; i < 4; i++)
                {
                    ci.DrawSetTransform(c + new Vector2((i - 1.5f) * r * 0.28f, r * 0.08f), (i - 1.5f) * 0.25f, Vector2.One);
                    var card = new Rect2(-r * 0.2f, -r * 0.32f, r * 0.4f, r * 0.62f);
                    ci.DrawRect(card, i == 3 ? light : white);
                    ci.DrawRect(card, dark, false, 1.5f);
                }
                ci.DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
                break;

            case UpgradeIcon.Heart:
            {
                var pts = new Vector2[24];
                for (int i = 0; i < pts.Length; i++)
                {
                    float t = i / (float)pts.Length * Mathf.Tau;
                    float x = 16f * Mathf.Pow(Mathf.Sin(t), 3f);
                    float y = 13f * Mathf.Cos(t) - 5f * Mathf.Cos(2f * t) - 2f * Mathf.Cos(3f * t) - Mathf.Cos(4f * t);
                    pts[i] = c + new Vector2(x, -y) * r * 0.034f;
                }
                ci.DrawColoredPolygon(pts, light);
                ci.DrawCircle(c + new Vector2(-r * 0.18f, -r * 0.14f), r * 0.1f, white);
                break;
            }

            case UpgradeIcon.Gear:
                for (int i = 0; i < 8; i++)
                {
                    float a = i * Mathf.Tau / 8f;
                    var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    ci.DrawLine(c + d * r * 0.3f, c + d * r * 0.62f, white, r * 0.18f);
                }
                ci.DrawCircle(c, r * 0.42f, white);
                ci.DrawCircle(c, r * 0.18f, dark);
                ci.DrawCircle(c + new Vector2(r * 0.55f, r * 0.5f), r * 0.14f, light);
                break;

            case UpgradeIcon.Clock:
                ci.DrawArc(c, r * 0.58f, 0f, Mathf.Tau, 40, white, r * 0.1f, true);
                ci.DrawLine(c, c + new Vector2(0f, -r * 0.4f), white, r * 0.09f);
                ci.DrawLine(c, c + new Vector2(r * 0.3f, 0f), light, r * 0.09f);
                ci.DrawCircle(c, r * 0.07f, white);
                break;

            case UpgradeIcon.Skull:
                ci.DrawCircle(c + new Vector2(0f, -r * 0.1f), r * 0.45f, white);
                ci.DrawRect(new Rect2(c + new Vector2(-r * 0.25f, r * 0.2f), new Vector2(r * 0.5f, r * 0.3f)), white);
                ci.DrawCircle(c + new Vector2(-r * 0.17f, -r * 0.1f), r * 0.12f, dark);
                ci.DrawCircle(c + new Vector2(r * 0.17f, -r * 0.1f), r * 0.12f, dark);
                break;
        }
    }

    private static void Plus(CanvasItem ci, Vector2 c, float r, Color color)
    {
        ci.DrawLine(c - new Vector2(r, 0f), c + new Vector2(r, 0f), color, r * 0.5f);
        ci.DrawLine(c - new Vector2(0f, r), c + new Vector2(0f, r), color, r * 0.5f);
    }

    private static void Cross(CanvasItem ci, Vector2 c, float r, Color color, float width)
    {
        ci.DrawLine(c + new Vector2(-r, -r), c + new Vector2(r, r), color, width);
        ci.DrawLine(c + new Vector2(-r, r), c + new Vector2(r, -r), color, width);
    }

    private static void Arrow(CanvasItem ci, Vector2 from, Vector2 to, Color color)
    {
        var dir = (to - from).Normalized();
        float len = (to - from).Length();
        var side = new Vector2(-dir.Y, dir.X) * len * 0.35f;
        ci.DrawLine(from, to - dir * len * 0.2f, color, len * 0.16f);
        ci.DrawColoredPolygon(new[] { to, to - dir * len * 0.45f + side, to - dir * len * 0.45f - side }, color);
    }

    private static Vector2[] StarPoints(Vector2 c, float outer, float inner)
    {
        var pts = new Vector2[10];
        for (int i = 0; i < 10; i++)
        {
            float a = -Mathf.Pi / 2f + i * Mathf.Pi / 5f;
            float r = i % 2 == 0 ? outer : inner;
            pts[i] = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
        }
        return pts;
    }
}
