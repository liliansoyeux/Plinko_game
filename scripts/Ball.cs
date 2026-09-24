using Godot;

namespace Plinko;

public partial class Ball : RigidBody2D
{
    public float Radius = 9f;
    public bool IsGolden;
    public int RunId;
    public readonly System.Collections.Generic.HashSet<int> PortalsUsed = new();

    private const double StuckThresholdSeconds = 0.4;
    private const float StuckSpeedThreshold = 4f;

    // Separate check for a ball rolling back and forth on top of a peg/blocker at a
    // roughly constant height — it's technically "moving" (so the check above never
    // fires) but never actually falling, so it can loop there forever otherwise.
    private const double YStuckThresholdSeconds = 1.2;
    private const float YStuckThreshold = 3f;
    private const double MaxLifetimeSeconds = 25.0;
    private const int TrailLength = 12;

    private Vector2 _lastPosition;
    private double _stuckTime;
    private float _lastY;
    private double _yStuckTime;
    private double _age;
    private bool _settled;
    private readonly Vector2[] _trail = new Vector2[TrailLength];
    private int _trailCount;
    private float _spawnScale;

    public override void _Ready()
    {
        // Balls only collide with the board (pegs/blockers/walls), never with each other,
        // so they can't jam or stack on top of one another mid-fall.
        CollisionLayer = PhysicsLayers.Ball;
        CollisionMask = PhysicsLayers.Board;
        AddChild(new CollisionShape2D { Shape = new CircleShape2D { Radius = Radius } });

        GravityScale = 1f;
        LockRotation = true;
        ContinuousCd = CcdMode.CastShape;
        ContactMonitor = true;
        MaxContactsReported = 4;
        PhysicsMaterialOverride = new PhysicsMaterial { Bounce = 0.22f, Friction = 0.3f };
        BodyEntered += OnBodyEntered;

        _lastPosition = Position;
        _lastY = Position.Y;
        ZIndex = 5;
    }

    private void OnBodyEntered(Node body)
    {
        if (body is Peg peg)
        {
            peg.Hit(IsGolden);
            float pitch = 0.85f + GD.Randf() * 0.4f + Mathf.Clamp(GlobalPosition.Y / 1600f, 0f, 0.4f);
            Sfx.Play(Sound.Peg, pitch, -9f);
            RunManager.Instance.RegisterPegHit(this);
        }
        else if (body is Blocker blocker)
        {
            blocker.Hit();
            Sfx.Play(Sound.Peg, 0.55f + GD.Randf() * 0.1f, -6f);
        }
    }

    public override void _Process(double delta)
    {
        _spawnScale = Mathf.Min(1f, _spawnScale + (float)delta * 7f);

        // Trail stored in global space, drawn relative to the ball each frame.
        for (int i = TrailLength - 1; i > 0; i--)
        {
            _trail[i] = _trail[i - 1];
        }
        _trail[0] = GlobalPosition;
        _trailCount = Mathf.Min(TrailLength, _trailCount + 1);
        QueueRedraw();
    }

    public override void _Draw()
    {
        var core = IsGolden ? new Color(1f, 0.8f, 0.3f) : new Color(0.98f, 0.93f, 1f);
        var glow = IsGolden ? Pal.Gold : Pal.Pink;

        for (int i = 1; i < _trailCount; i++)
        {
            float t = 1f - i / (float)TrailLength;
            var local = _trail[i] - GlobalPosition;
            DrawCircle(local, Radius * (0.25f + 0.6f * t), Pal.Alpha(Pal.Hdr(glow, 1.4f), 0.22f * t * t));
        }

        float r = Radius * (0.4f + 0.6f * _spawnScale);
        Paint.Halo(this, Vector2.Zero, r * 2.4f, Pal.Alpha(glow, IsGolden ? 0.55f : 0.35f));
        DrawCircle(Vector2.Zero, r, IsGolden ? Pal.Hdr(core, 1.35f) : Pal.Hdr(core, 1.1f));
        DrawCircle(new Vector2(0f, r * 0.18f), r * 0.82f, Pal.Alpha(IsGolden ? new Color(0.8f, 0.5f, 0.1f) : new Color(0.75f, 0.68f, 0.85f), 0.45f));
        DrawCircle(new Vector2(-r * 0.32f, -r * 0.36f), r * 0.32f, Pal.Hdr(Colors.White, 1.6f));
    }

    public override void _PhysicsProcess(double delta)
    {
        _age += delta;
        if (_age > MaxLifetimeSeconds || Position.Y > 4000f)
        {
            // Last-resort safety: a ball wedged somewhere the unstick hop can't fix.
            RunManager.Instance.RegisterMiss(this);
            Settle();
            return;
        }

        float speed = Position.DistanceTo(_lastPosition) / (float)delta;
        _lastPosition = Position;

        if (speed < StuckSpeedThreshold)
        {
            _stuckTime += delta;
            if (_stuckTime >= StuckThresholdSeconds)
            {
                JumpToUnstick();
                _stuckTime = 0;
            }
        }
        else
        {
            _stuckTime = 0;
        }

        float yDelta = Mathf.Abs(Position.Y - _lastY);
        if (yDelta < YStuckThreshold)
        {
            _yStuckTime += delta;
            if (_yStuckTime >= YStuckThresholdSeconds)
            {
                JumpToUnstick();
                _yStuckTime = 0;
            }
        }
        else
        {
            _yStuckTime = 0;
            _lastY = Position.Y;
        }
    }

    private void JumpToUnstick()
    {
        // A clear upward hop (set directly, not added as an impulse) so it always reads
        // as a visible "jump" regardless of whatever tiny residual velocity it had.
        float direction = GD.Randf() < 0.5f ? -1f : 1f;
        LinearVelocity = new Vector2(direction * 90f, -380f);
    }

    public void Settle()
    {
        if (_settled)
        {
            return;
        }
        _settled = true;
        QueueFree();
    }

    public override void _ExitTree()
    {
        // Fires no matter how the ball was removed (slot, floor catcher, safety despawn),
        // so RunManager always knows when a palier's balls have all settled.
        RunManager.Instance?.NotifyBallSettled(RunId);
    }
}
