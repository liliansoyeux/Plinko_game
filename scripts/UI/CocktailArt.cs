using Godot;

namespace Plinko;

// Little cocktail glasses, one silhouette per drink, drawn standing on a base point.
public static class CocktailArt
{
    private enum Glass { Martini, Highball, Rocks, Coupe, Wine }

    private static Glass GlassFor(string id) => id switch
    {
        "mojito" => Glass.Highball,
        "tequila_sunrise" => Glass.Highball,
        "blue_lagoon" => Glass.Martini,
        "cosmopolitan" => Glass.Martini,
        "pina_colada" => Glass.Coupe,
        "spritz" => Glass.Wine,
        _ => Glass.Rocks,
    };

    public static void DrawGlass(CanvasItem ci, Vector2 basePoint, float scale, IRunModifier cocktail, float shimmer)
    {
        var liquid = cocktail.LiquidColor;
        var glassLine = new Color(0.92f, 0.95f, 1f, 0.85f);
        var glassFill = new Color(0.8f, 0.9f, 1f, 0.12f);
        Vector2 P(float x, float y) => basePoint + new Vector2(x, y) * scale;

        Paint.Halo(ci, P(0f, -18f), 26f * scale, Pal.Alpha(liquid, 0.18f + 0.1f * shimmer), 4);

        switch (GlassFor(cocktail.Id))
        {
            case Glass.Martini:
            case Glass.Coupe:
            {
                bool coupe = GlassFor(cocktail.Id) == Glass.Coupe;
                ci.DrawLine(P(-9f, 0f), P(9f, 0f), glassLine, 2f * scale);
                ci.DrawLine(P(0f, 0f), P(0f, -14f), glassLine, 2f * scale);
                if (coupe)
                {
                    ci.DrawColoredPolygon(new[] { P(-13f, -30f), P(13f, -30f), P(9f, -19f), P(0f, -15f), P(-9f, -19f) }, glassFill);
                    ci.DrawColoredPolygon(new[] { P(-11f, -27f), P(11f, -27f), P(8f, -20f), P(0f, -16f), P(-8f, -20f) }, Pal.Hdr(liquid, 1.1f));
                    ci.DrawPolyline(new[] { P(-13f, -30f), P(-9f, -19f), P(0f, -14f), P(9f, -19f), P(13f, -30f) }, glassLine, 1.5f * scale, true);
                    // Umbrella.
                    ci.DrawLine(P(4f, -26f), P(10f, -42f), new Color(0.9f, 0.85f, 0.7f), 1.2f * scale);
                    ci.DrawColoredPolygon(new[] { P(1f, -41f), P(19f, -44f), P(10f, -50f) }, Pal.Hdr(Pal.Pink, 1.2f));
                }
                else
                {
                    ci.DrawColoredPolygon(new[] { P(-15f, -34f), P(15f, -34f), P(0f, -14f) }, glassFill);
                    ci.DrawColoredPolygon(new[] { P(-12f, -30f), P(12f, -30f), P(0f, -16f) }, Pal.Hdr(liquid, 1.1f));
                    ci.DrawPolyline(new[] { P(-15f, -34f), P(0f, -14f), P(15f, -34f) }, glassLine, 1.5f * scale, true);
                    ci.DrawCircle(P(7f, -31f), 2.6f * scale, Pal.Hdr(new Color(0.5f, 0.9f, 0.3f), 1.1f));
                }
                break;
            }
            case Glass.Highball:
            {
                var r = new Rect2(P(-8f, -38f), new Vector2(16f, 38f) * scale);
                ci.DrawRect(r, glassFill);
                if (cocktail.Id == "tequila_sunrise")
                {
                    ci.DrawPolygon(new[] { P(-7f, -30f), P(7f, -30f), P(7f, -2f), P(-7f, -2f) },
                        new[] { Pal.Hdr(Pal.Gold, 1.1f), Pal.Hdr(Pal.Gold, 1.1f), Pal.Hdr(Pal.Red, 1.1f), Pal.Hdr(Pal.Red, 1.1f) });
                }
                else
                {
                    ci.DrawRect(new Rect2(P(-7f, -30f), new Vector2(14f, 28f) * scale), Pal.Hdr(liquid, 0.9f));
                    ci.DrawCircle(P(-3f, -26f), 3f * scale, new Color(0.3f, 0.8f, 0.3f));
                    ci.DrawCircle(P(3f, -18f), 2.4f * scale, new Color(1f, 1f, 1f, 0.5f));
                }
                ci.DrawRect(r, glassLine, false, 1.5f * scale);
                ci.DrawLine(P(3f, -30f), P(8f, -46f), Pal.Hdr(Pal.Cyan, 1.2f), 1.6f * scale);
                break;
            }
            case Glass.Wine:
            {
                ci.DrawLine(P(-8f, 0f), P(8f, 0f), glassLine, 2f * scale);
                ci.DrawLine(P(0f, 0f), P(0f, -14f), glassLine, 2f * scale);
                var bowl = new[] { P(-10f, -38f), P(10f, -38f), P(11f, -26f), P(6f, -16f), P(0f, -14f), P(-6f, -16f), P(-11f, -26f) };
                ci.DrawColoredPolygon(bowl, glassFill);
                ci.DrawColoredPolygon(new[] { P(-10f, -28f), P(10f, -28f), P(6f, -17f), P(0f, -15f), P(-6f, -17f) }, Pal.Hdr(liquid, 1.1f));
                ci.DrawPolyline(new[] { bowl[0], bowl[6], bowl[5], bowl[4], bowl[3], bowl[2], bowl[1] }, glassLine, 1.5f * scale, true);
                ci.DrawCircle(P(8f, -30f), 4f * scale, Pal.Hdr(Pal.Orange, 1.1f));
                break;
            }
            default:
            {
                var r = new Rect2(P(-11f, -22f), new Vector2(22f, 22f) * scale);
                ci.DrawRect(r, glassFill);
                ci.DrawRect(new Rect2(P(-10f, -16f), new Vector2(20f, 15f) * scale), Pal.Hdr(liquid, 0.9f));
                ci.DrawRect(new Rect2(P(-6f, -15f), new Vector2(10f, 9f) * scale), new Color(1f, 1f, 1f, 0.35f));
                ci.DrawRect(r, glassLine, false, 1.5f * scale);
                ci.DrawLine(P(-11f, 0f), P(11f, 0f), glassLine, 3f * scale);
                ci.DrawLine(P(6f, -18f), P(12f, -24f), Pal.Hdr(Pal.Orange, 1.2f), 3f * scale);
                break;
            }
        }
    }
}

public partial class CocktailGlass : Control
{
    public IRunModifier Cocktail;
    private float _time;
    private float _appear;

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(42f, 56f);
        TooltipText = $"{Cocktail.DisplayName}\n{Cocktail.Description}";
        MouseFilter = MouseFilterEnum.Stop;
        _time = GD.Randf() * 5f;
    }

    public override void _Process(double delta)
    {
        _time += (float)delta;
        _appear = Mathf.Min(1f, _appear + (float)delta * 2.5f);
        QueueRedraw();
    }

    public override void _Draw()
    {
        float t = _appear - 1f;
        float s = 1f + 2.7f * t * t * t + 1.7f * t * t;
        DrawSetTransform(new Vector2(Size.X / 2f, Size.Y - 2f), 0f, new Vector2(s, s));
        CocktailArt.DrawGlass(this, Vector2.Zero, 1f, Cocktail, 0.5f + 0.5f * Mathf.Sin(_time * 2f));
        DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
    }
}
