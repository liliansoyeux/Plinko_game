using Godot;
using System.Collections.Generic;

namespace Plinko;

// Shared visual language: colors, fonts, theme and small drawing helpers. Everything in the
// game is drawn in code, so keeping the palette in one place is what makes it read as one piece.
public static class Pal
{
    public static readonly Color Night = new(0.035f, 0.02f, 0.065f);
    public static readonly Color Wine = new(0.17f, 0.04f, 0.11f);
    public static readonly Color Panel = new(0.085f, 0.05f, 0.13f, 0.96f);
    public static readonly Color PanelLight = new(0.17f, 0.1f, 0.25f);
    public static readonly Color Gold = new(1f, 0.78f, 0.28f);
    public static readonly Color Pink = new(1f, 0.27f, 0.62f);
    public static readonly Color Cyan = new(0.28f, 0.9f, 1f);
    public static readonly Color Green = new(0.38f, 1f, 0.56f);
    public static readonly Color Red = new(1f, 0.27f, 0.32f);
    public static readonly Color Orange = new(1f, 0.55f, 0.2f);
    public static readonly Color Purple = new(0.68f, 0.4f, 1f);
    public static readonly Color Text = new(0.97f, 0.94f, 0.99f);
    public static readonly Color TextDim = new(0.72f, 0.66f, 0.8f);
    public static readonly Color Legendary = new(1f, 0.45f, 0.92f);

    // Slowly cycling rainbow, reserved for legendary things.
    public static Color Prismatic(float time, float offset = 0f) =>
        Color.FromHsv(Mathf.PosMod(time * 0.25f + offset, 1f), 0.55f, 1f);

    public static Color Hdr(Color c, float k) => new(c.R * k, c.G * k, c.B * k, c.A);
    public static Color Alpha(Color c, float a) => new(c.R, c.G, c.B, a);

    public static Color ForRarity(Rarity rarity) => rarity switch
    {
        Rarity.Legendary => Legendary,
        Rarity.Epic => Gold,
        Rarity.Rare => Cyan,
        Rarity.Cursed => Purple,
        _ => new Color(0.8f, 0.76f, 0.9f)
    };

    public static string RarityLabel(Rarity rarity) => rarity switch
    {
        Rarity.Legendary => "LÉGENDAIRE",
        Rarity.Epic => "ÉPIQUE",
        Rarity.Rare => "RARE",
        Rarity.Cursed => "MAUDIT",
        _ => "COMMUN"
    };

    public static Color ForMultiplier(float m)
    {
        if (m <= 0f) return new Color(0.35f, 0.3f, 0.4f);
        if (m < 0.25f) return new Color(0.85f, 0.12f, 0.2f);
        if (m < 0.6f) return Red;
        if (m < 1f) return Orange;
        if (m == 1f) return new Color(0.72f, 0.68f, 0.82f);
        if (m < 4f) return Cyan;
        if (m < 8f) return Green;
        if (m < 20f) return Purple;
        if (m < 60f) return Pink;
        return Gold;
    }

    public static string FormatMultiplier(float value)
    {
        if (value >= 10f || Mathf.IsEqualApprox(value, Mathf.Round(value)))
        {
            return $"x{Mathf.RoundToInt(value)}";
        }
        return $"x{value:0.#}".Replace(',', '.');
    }

    public static string FormatScore(float value)
    {
        if (value >= 1_000_000f) return $"{value / 1_000_000f:0.##}M".Replace(',', '.');
        if (value >= 10_000f) return $"{value / 1000f:0.#}k".Replace(',', '.');
        return $"{value:0}";
    }
}

public static class Fonts
{
    private static Font _regular;
    private static Font _bold;
    private static Font _black;

    public static Font Regular => _regular ??= Make(new[] { "Bahnschrift", "Segoe UI", "Arial" }, 400);
    public static Font Bold => _bold ??= Make(new[] { "Bahnschrift SemiBold", "Bahnschrift", "Segoe UI Semibold", "Arial" }, 700);
    public static Font Black => _black ??= Make(new[] { "Impact", "Arial Black", "Bahnschrift" }, 900);

    private static Font Make(string[] names, int weight)
    {
        var font = new SystemFont
        {
            FontNames = names,
            FontWeight = weight,
            Antialiasing = TextServer.FontAntialiasing.Gray,
            SubpixelPositioning = TextServer.SubpixelPositioning.Disabled,
            MultichannelSignedDistanceField = false,
        };
        font.Fallbacks = new Godot.Collections.Array<Font>
        {
            new SystemFont { FontNames = new[] { "Segoe UI Symbol", "Segoe UI" } }
        };
        return font;
    }
}

public static class UiTheme
{
    public static StyleBoxFlat Box(Color bg, Color border, int borderWidth = 2, int radius = 12, int margin = 12)
    {
        var box = new StyleBoxFlat
        {
            BgColor = bg,
            BorderColor = border,
            AntiAliasing = true,
        };
        box.SetBorderWidthAll(borderWidth);
        box.SetCornerRadiusAll(radius);
        box.SetContentMarginAll(margin);
        return box;
    }

    public static Theme Build()
    {
        var t = new Theme
        {
            DefaultFont = Fonts.Regular,
            DefaultFontSize = 18,
        };

        var buttonNormal = Box(Pal.PanelLight, Pal.Alpha(Pal.Pink, 0.55f), 2, 12, 10);
        buttonNormal.ContentMarginLeft = buttonNormal.ContentMarginRight = 22;
        var buttonHover = (StyleBoxFlat)buttonNormal.Duplicate();
        buttonHover.BgColor = new Color(0.27f, 0.12f, 0.33f);
        buttonHover.BorderColor = Pal.Pink;
        buttonHover.ShadowColor = Pal.Alpha(Pal.Pink, 0.35f);
        buttonHover.ShadowSize = 10;
        var buttonPressed = (StyleBoxFlat)buttonNormal.Duplicate();
        buttonPressed.BgColor = new Color(0.45f, 0.1f, 0.3f);
        buttonPressed.BorderColor = Pal.Gold;
        var buttonFocus = Box(new Color(0, 0, 0, 0), Pal.Alpha(Pal.Gold, 0.8f), 2, 12, 10);
        var buttonDisabled = (StyleBoxFlat)buttonNormal.Duplicate();
        buttonDisabled.BgColor = new Color(0.12f, 0.1f, 0.14f);
        buttonDisabled.BorderColor = new Color(0.3f, 0.28f, 0.34f);

        t.SetStylebox("normal", "Button", buttonNormal);
        t.SetStylebox("hover", "Button", buttonHover);
        t.SetStylebox("pressed", "Button", buttonPressed);
        t.SetStylebox("hover_pressed", "Button", buttonPressed);
        t.SetStylebox("focus", "Button", buttonFocus);
        t.SetStylebox("disabled", "Button", buttonDisabled);
        t.SetFont("font", "Button", Fonts.Bold);
        t.SetFontSize("font_size", "Button", 20);
        t.SetColor("font_color", "Button", Pal.Text);
        t.SetColor("font_hover_color", "Button", Colors.White);
        t.SetColor("font_pressed_color", "Button", Pal.Gold);
        t.SetColor("font_focus_color", "Button", Colors.White);
        t.SetColor("font_disabled_color", "Button", new Color(0.5f, 0.47f, 0.55f));

        t.SetColor("font_color", "Label", Pal.Text);
        t.SetColor("font_shadow_color", "Label", new Color(0, 0, 0, 0.55f));
        t.SetConstant("shadow_offset_x", "Label", 0);
        t.SetConstant("shadow_offset_y", "Label", 2);

        var panel = Box(Pal.Panel, Pal.Alpha(Pal.Purple, 0.6f), 2, 16, 18);
        panel.ShadowColor = new Color(0, 0, 0, 0.5f);
        panel.ShadowSize = 18;
        t.SetStylebox("panel", "PanelContainer", panel);
        t.SetStylebox("panel", "Panel", panel);

        t.SetStylebox("background", "ProgressBar", Box(new Color(0.03f, 0.02f, 0.05f, 0.9f), new Color(1, 1, 1, 0.12f), 1, 7, 0));
        t.SetStylebox("fill", "ProgressBar", Box(Pal.Green, new Color(1, 1, 1, 0.25f), 0, 7, 0));

        var tooltip = Box(new Color(0.06f, 0.03f, 0.09f, 0.97f), Pal.Alpha(Pal.Gold, 0.7f), 2, 10, 10);
        t.SetStylebox("panel", "TooltipPanel", tooltip);
        t.SetColor("font_color", "TooltipLabel", Pal.Text);
        t.SetFont("font", "TooltipLabel", Fonts.Regular);
        t.SetFontSize("font_size", "TooltipLabel", 16);

        return t;
    }
}

public static class Ui
{
    public static Label Label(string text, int size, Color? color = null, Font font = null,
        HorizontalAlignment align = HorizontalAlignment.Left, int outline = 0)
    {
        var settings = new LabelSettings
        {
            Font = font ?? Fonts.Bold,
            FontSize = size,
            FontColor = color ?? Pal.Text,
            ShadowColor = new Color(0, 0, 0, 0.55f),
            ShadowOffset = new Vector2(0, 2),
            ShadowSize = 1,
        };
        if (outline > 0)
        {
            settings.OutlineSize = outline;
            settings.OutlineColor = new Color(0.05f, 0.02f, 0.08f);
        }
        return new Label
        {
            Text = text,
            LabelSettings = settings,
            HorizontalAlignment = align,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
    }

    // Autowrap must be on before the size is set: a non-wrapping label's minimum width is
    // its whole text, and Godot silently clamps a smaller Size back up to that.
    public static Label Wrapped(string text, int size, Color color, Font font, HorizontalAlignment align,
        Vector2 position, Vector2 boxSize, int outline = 0, VerticalAlignment valign = VerticalAlignment.Top)
    {
        var label = Label(text, size, color, font, align, outline);
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        label.VerticalAlignment = valign;
        label.Position = position;
        label.Size = boxSize;
        return label;
    }

    // Containers default to swallowing mouse clicks, which would stop board clicks from ever
    // reaching _UnhandledInput. Every decorative control gets Ignore; only real buttons stop.
    public static void IgnoreMouseRecursive(Node node)
    {
        if (node is Control control && node is not BaseButton && string.IsNullOrEmpty(control.TooltipText))
        {
            control.MouseFilter = Control.MouseFilterEnum.Ignore;
        }
        foreach (Node child in node.GetChildren())
        {
            IgnoreMouseRecursive(child);
        }
    }

    public static Tween PopIn(Control control, float delay = 0f, float duration = 0.35f)
    {
        control.PivotOffset = control.Size / 2f;
        control.Scale = new Vector2(0.6f, 0.6f);
        control.Modulate = new Color(1, 1, 1, 0);
        var tween = control.CreateTween().SetParallel(true);
        tween.SetPauseMode(Tween.TweenPauseMode.Process);
        tween.TweenProperty(control, "scale", Vector2.One, duration).SetDelay(delay)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(control, "modulate:a", 1f, duration * 0.7f).SetDelay(delay);
        return tween;
    }
}

public static class FxTextures
{
    private static Texture2D _softDot;
    private static Texture2D _spark;

    // Radial falloff dot used by every particle system, so particles read as light rather
    // than as the square pixels CPUParticles2D draws by default.
    public static Texture2D SoftDot => _softDot ??= MakeDot(32, 2.2f);
    public static Texture2D Spark => _spark ??= MakeDot(16, 1.2f);

    private static Texture2D MakeDot(int size, float falloff)
    {
        var image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        float r = size / 2f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = new Vector2(x + 0.5f - r, y + 0.5f - r).Length() / r;
                float a = Mathf.Clamp(1f - d, 0f, 1f);
                a = Mathf.Pow(a, falloff);
                image.SetPixel(x, y, new Color(1, 1, 1, a));
            }
        }
        return ImageTexture.CreateFromImage(image);
    }
}

public static class Paint
{

    // Soft glow: a few concentric translucent discs. Cheap and reads well with HDR bloom.
    public static void Halo(CanvasItem ci, Vector2 center, float radius, Color color, int layers = 4)
    {
        for (int i = layers; i >= 1; i--)
        {
            float t = i / (float)layers;
            ci.DrawCircle(center, radius * t, Pal.Alpha(color, color.A * (1f - t * 0.85f) / layers * 1.6f));
        }
    }

    public static Vector2[] RoundedRect(Rect2 rect, float radius, int segments = 5)
    {
        radius = Mathf.Min(radius, Mathf.Min(rect.Size.X, rect.Size.Y) / 2f);
        var pts = new List<Vector2>();
        var corners = new[]
        {
            (rect.Position + new Vector2(rect.Size.X - radius, radius), -Mathf.Pi / 2f),
            (rect.End - new Vector2(radius, radius), 0f),
            (new Vector2(rect.Position.X + radius, rect.End.Y - radius), Mathf.Pi / 2f),
            (rect.Position + new Vector2(radius, radius), Mathf.Pi),
        };
        foreach (var (c, start) in corners)
        {
            for (int i = 0; i <= segments; i++)
            {
                float a = start + i / (float)segments * Mathf.Pi / 2f;
                pts.Add(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius);
            }
        }
        return pts.ToArray();
    }

    public static Vector2[] Closed(Vector2[] points)
    {
        var closed = new Vector2[points.Length + 1];
        points.CopyTo(closed, 0);
        closed[^1] = points[0];
        return closed;
    }

    public static void VerticalGradient(CanvasItem ci, Rect2 rect, Color top, Color bottom)
    {
        var pts = new[] { rect.Position, new Vector2(rect.End.X, rect.Position.Y), rect.End, new Vector2(rect.Position.X, rect.End.Y) };
        ci.DrawPolygon(pts, new[] { top, top, bottom, bottom });
    }

    public static void TextCentered(CanvasItem ci, Font font, Vector2 center, string text, int size, Color color, int outline = 0, Color? outlineColor = null)
    {
        var textSize = font.GetStringSize(text, HorizontalAlignment.Left, -1, size);
        var pos = center + new Vector2(-textSize.X / 2f, font.GetAscent(size) - textSize.Y / 2f);
        if (outline > 0)
        {
            ci.DrawStringOutline(font, pos, text, HorizontalAlignment.Left, -1, size, outline, outlineColor ?? new Color(0.03f, 0.01f, 0.05f));
        }
        ci.DrawString(font, pos, text, HorizontalAlignment.Left, -1, size, color);
    }
}
