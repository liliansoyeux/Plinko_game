using Godot;

namespace Plinko;

public static class Fx
{
    public static void Burst(Node parent, Vector2 position, Color color, int amount = 24, float speed = 220f,
        float lifetime = 0.7f, float spread = 180f, Vector2? direction = null, float scale = 1f)
    {
        var ramp = new Gradient();
        ramp.SetColor(0, Pal.Hdr(color, 2.2f));
        ramp.SetColor(1, Pal.Alpha(color, 0f));

        var particles = new CpuParticles2D
        {
            Position = position,
            Emitting = false,
            OneShot = true,
            Amount = amount,
            Lifetime = lifetime,
            Explosiveness = 0.95f,
            Texture = FxTextures.SoftDot,
            Direction = direction ?? Vector2.Up,
            Spread = spread,
            InitialVelocityMin = speed * 0.35f,
            InitialVelocityMax = speed,
            Gravity = new Vector2(0f, 420f),
            DampingMin = 20f,
            DampingMax = 60f,
            ScaleAmountMin = 0.18f * scale,
            ScaleAmountMax = 0.45f * scale,
            ColorRamp = ramp,
            ZIndex = 30,
            LocalCoords = false,
        };
        parent.AddChild(particles);
        particles.Emitting = true;
        particles.Finished += particles.QueueFree;
    }

    public static void FloatText(Node parent, Vector2 position, string text, Color color, int size = 22, float rise = 55f, float duration = 0.95f)
    {
        var node = new FloatingText { Position = position, Text = text, Color = color, FontSize = size };
        parent.AddChild(node);
        node.Play(rise, duration);
    }
}

public partial class FloatingText : Node2D
{
    public string Text = "";
    public Color Color = Colors.White;
    public int FontSize = 22;

    private float _scale = 0.4f;

    public override void _Ready()
    {
        ZIndex = 40;
    }

    public void Play(float rise, float duration)
    {
        var tween = CreateTween().SetParallel(true);
        tween.TweenProperty(this, "position:y", Position.Y - rise, duration).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        tween.TweenMethod(Callable.From<float>(v => { _scale = v; QueueRedraw(); }), 0.4f, 1f, 0.25f)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(this, "modulate:a", 0f, duration * 0.4f).SetDelay(duration * 0.6f);
        tween.Chain().TweenCallback(Callable.From(QueueFree));
    }

    public override void _Draw()
    {
        DrawSetTransform(Vector2.Zero, 0f, new Vector2(_scale, _scale));
        Paint.TextCentered(this, Fonts.Black, Vector2.Zero, Text, FontSize, Pal.Hdr(Color, 1.5f), 5, new Color(0.05f, 0.01f, 0.08f, 0.9f));
        DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
    }
}

// Trauma-based screen shake: shake strength is trauma², so small hits barely move the
// screen while big jackpots really kick.
public partial class ShakeCamera : Camera2D
{
    private float _trauma;
    private readonly FastNoiseLite _noise = new() { Frequency = 0.9f };
    private float _t;

    public void AddTrauma(float amount)
    {
        _trauma = Mathf.Min(1f, _trauma + amount);
    }

    public override void _Process(double delta)
    {
        _t += (float)delta * 60f;
        _trauma = Mathf.Max(0f, _trauma - (float)delta * 1.6f);
        float shake = _trauma * _trauma;
        Offset = new Vector2(_noise.GetNoise2D(_t, 0f), _noise.GetNoise2D(0f, _t)) * 16f * shake;
        Rotation = _noise.GetNoise2D(_t, _t) * 0.02f * shake;
    }
}
