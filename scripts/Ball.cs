using Godot;
using System;
using System.Collections.Generic;

namespace Plinko;

public partial class Ball : RigidBody2D
{
    public event Action<Ball> PegHit;
    public event Action<Ball> Removed;

    public float Radius = 9f;
    public int Tier;
    public double Stack = 1.0;     // how many owned balls this physical ball stands for
    public int TokenIndex = -1;    // reserve slot it returns to; -1 for portal twins
    public bool IsTwin => TokenIndex < 0;
    public readonly HashSet<int> PortalsUsed = new();

    private const double StuckThresholdSeconds = 0.4;
    private const float StuckSpeedThreshold = 4f;

    // Separate check for a ball rolling back and forth on top of a peg at a roughly
    // constant height — it's technically "moving" but never actually falling.
    private const double YStuckThresholdSeconds = 1.2;
    private const float YStuckThreshold = 3f;
    private const double MaxLifetimeSeconds = 25.0;
    private const int TrailLength = 10;

    private Vector2 _lastPosition;
    private double _stuckTime;
    private float _lastY;
    private double _yStuckTime;
    private double _age;
    private bool _settled;
    private readonly Vector2[] _trail = new Vector2[TrailLength];
    private int _trailCount;
    private float _spawnScale;
    private float _time;

    public override void _Ready()
    {
        // Balls only collide with the board (pegs/walls), never with each other.
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
        _time = GD.Randf() * 10f;
        ZIndex = 5;
    }

    private void OnBodyEntered(Node body)
    {
        if (body is Peg peg)
        {
            peg.Hit(Tier >= 2);
            float pitch = 0.85f + GD.Randf() * 0.4f + Mathf.Clamp(GlobalPosition.Y / 1600f, 0f, 0.4f) + Tier * 0.05f;
            Sfx.Play(Sound.Peg, pitch, -12f);
            PegHit?.Invoke(this);
        }
    }

    public override void _Process(double delta)
    {
        _time += (float)delta;
        _spawnScale = Mathf.Min(1f, _spawnScale + (float)delta * 7f);
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
        var def = BallTiers.All[Tier];
        var glow = Tier == 5 ? Pal.Prismatic(_time) : def.Glow;

        for (int i = 1; i < _trailCount; i++)
        {
            float t = 1f - i / (float)TrailLength;
            DrawCircle(_trail[i] - GlobalPosition, Radius * (0.25f + 0.6f * t), Pal.Alpha(Pal.Hdr(glow, 1.4f), 0.2f * t * t));
        }

        float r = Radius * (0.4f + 0.6f * _spawnScale);
        Paint.Halo(this, Vector2.Zero, r * (2.2f + 0.2f * Tier), Pal.Alpha(glow, 0.3f + 0.06f * Tier));
        DrawCircle(Vector2.Zero, r, Pal.Hdr(def.Color, 1.1f + 0.05f * Tier));
        DrawCircle(new Vector2(0f, r * 0.18f), r * 0.82f, Pal.Alpha(def.Color.Darkened(0.35f), 0.45f));
        if (Tier == 3)
        {
            // Diamond facets.
            DrawLine(new Vector2(-r * 0.6f, 0f), new Vector2(r * 0.6f, 0f), Pal.Alpha(Colors.White, 0.5f), 1f);
            DrawLine(new Vector2(0f, -r * 0.6f), new Vector2(0f, r * 0.6f), Pal.Alpha(Colors.White, 0.35f), 1f);
        }
        DrawCircle(new Vector2(-r * 0.32f, -r * 0.36f), r * 0.32f, Pal.Hdr(Colors.White, 1.6f));
        if (IsTwin)
        {
            DrawArc(Vector2.Zero, r + 2f, 0f, Mathf.Tau, 20, Pal.Alpha(Pal.Prismatic(_time), 0.8f), 1.5f, true);
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        _age += delta;
        if (_age > MaxLifetimeSeconds || Position.Y > 4000f)
        {
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
        // Fires however the ball was removed, so its reserve token always comes back.
        Removed?.Invoke(this);
    }
}
