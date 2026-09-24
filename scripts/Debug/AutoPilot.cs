using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Plinko;

// Debug-only bot, enabled with `-- --autopilot` on the command line. Plays the game by
// itself (start, aim, drop, pick cards, restart), can take screenshots and prints a
// per-run summary — used to smoke-test and balance without a human at the keyboard.
//
//   --autopilot            enable
//   --runs=N               stop after N finished runs (default 1)
//   --char=<id>            shoes to use (default: random each run)
//   --shots=<dir>          save screenshots there (windowed runs only)
//   --shot-every=<sec>     screenshot interval (default 4)
//   --quit-after=<sec>     hard stop
//   --fast                 run the game at x2 speed
//   --pause-at=<sec>       open the pause menu once at that time (for screenshots)
//   --title-wait=<sec>     how long to stay on the title screen (default 1.5)
//   --jump=<palier>        jump straight to that palier number on the first run
//   --rows=<n>             force the peg row count on the first run
//   --give=<id,id,...>     apply these upgrade ids at the start of the first run
//   --chips=<n>            grant chips (in memory only; saving is disabled for the bot)
//   --skills=<n>           visit the skill tree first and buy up to n random nodes
//   --dump-sfx=<dir>       write every synthesized sound as a .wav
//   --mouse                drive everything with synthetic mouse clicks instead of calling
//                          methods directly (validates GUI/mouse-filter routing)
public partial class AutoPilot : Node
{
    private readonly Dictionary<string, string> _args = new();
    private double _time;
    private double _stateTime;
    private string _lastState = "";
    private double _nextShot;
    private int _shotIndex;
    private int _runsDone;
    private int _runsWanted = 1;
    private double _quitAfter = -1;
    private double _pauseAt = -1;
    private bool _pauseDone;
    private double _dropTimer;
    private string _shotDir;
    private double _shotEvery = 4;
    private double _titleWait = 1.5;
    private int _skillsToBuy;
    private bool _skillsVisited;
    private bool _rerollTried;
    private readonly RandomNumberGenerator _rng = new();
    private readonly List<string> _summaries = new();

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        SaveData.Disabled = true;
        foreach (var arg in OS.GetCmdlineUserArgs())
        {
            var parts = arg.TrimStart('-').Split('=', 2);
            _args[parts[0]] = parts.Length > 1 ? parts[1] : "true";
        }

        _runsWanted = int.Parse(_args.GetValueOrDefault("runs", "1"));
        _quitAfter = double.Parse(_args.GetValueOrDefault("quit-after", "-1"), CultureInfo.InvariantCulture);
        _pauseAt = double.Parse(_args.GetValueOrDefault("pause-at", "-1"), CultureInfo.InvariantCulture);
        _shotEvery = double.Parse(_args.GetValueOrDefault("shot-every", "4"), CultureInfo.InvariantCulture);
        _titleWait = double.Parse(_args.GetValueOrDefault("title-wait", "1.5"), CultureInfo.InvariantCulture);
        _nextShot = 1.0;
        if (_args.TryGetValue("shots", out var dir) && DisplayServer.GetName() != "headless")
        {
            _shotDir = dir;
            DirAccess.MakeDirRecursiveAbsolute(_shotDir);
        }
        if (_args.ContainsKey("seed"))
        {
            _rng.Seed = ulong.Parse(_args["seed"]);
        }
        if (_args.TryGetValue("dump-sfx", out var sfxDir))
        {
            Sfx.DumpAll(sfxDir);
            GD.Print($"[AutoPilot] sounds written to {sfxDir}");
        }
        if (_args.TryGetValue("chips", out var chips))
        {
            SaveData.AddChips(int.Parse(chips));
        }
        _skillsToBuy = int.Parse(_args.GetValueOrDefault("skills", "0"));
        GD.Print($"[AutoPilot] enabled: runs={_runsWanted} shots={_shotDir ?? "off"}");
    }

    public override void _Process(double delta)
    {
        double real = delta / Math.Max(0.01, Engine.TimeScale);
        _time += real;

        if (_shotDir != null && _time >= _nextShot)
        {
            _nextShot = _time + _shotEvery;
            Screenshot();
        }

        if (_quitAfter > 0 && _time > _quitAfter)
        {
            Finish("quit-after reached");
            return;
        }

        var main = Main.Instance;
        if (main == null || main.IsTransitioning)
        {
            return;
        }

        string state = main.Title != null ? "title"
            : main.Skills != null ? "skills"
            : main.Game == null ? "none"
            : main.Game.IsGameOver ? "gameover"
            : main.Game.IsPlacing ? "placing"
            : main.Game.Popup.IsOpen ? "popup"
            : main.Game.IsUserPaused ? "paused"
            : "playing";
        if (state != _lastState)
        {
            _lastState = state;
            _stateTime = 0;
        }
        _stateTime += real;

        switch (state)
        {
            case "title" when _skillsToBuy > 0 && !_skillsVisited && _stateTime > _titleWait:
                _skillsVisited = true;
                main.ShowSkillTree();
                break;

            case "skills" when _stateTime > 1.0:
                var affordable = new List<SkillNodeCard>();
                foreach (var card in main.Skills.Cards)
                {
                    if (SkillTree.CanBuy(card.Node)) affordable.Add(card);
                }
                if (_skillsToBuy > 0 && affordable.Count > 0)
                {
                    var chosenCard = affordable[_rng.RandiRange(0, affordable.Count - 1)];
                    if (Mouse)
                    {
                        Click(chosenCard.GetGlobalRect().GetCenter());
                    }
                    else
                    {
                        main.Skills.OnBuy(chosenCard);
                    }
                    _skillsToBuy--;
                    _stateTime = 0.6;
                }
                else if (_stateTime > 3.0)
                {
                    main.ShowTitle();
                }
                break;

            case "title" when _stateTime > _titleWait:
                if (Mouse)
                {
                    int index = _args.TryGetValue("char", out var id) ? Characters.All.FindIndex(c => c.Id == id) : _rng.RandiRange(0, Characters.All.Count - 1);
                    Click(new Vector2(30f + 80f + index * 170f, 580f));
                    Click(main.Title.PlayButton.GetGlobalRect().GetCenter());
                    _stateTime = -10;
                }
                else
                {
                    main.StartGame(PickCharacter());
                }
                break;

            case "popup" when _stateTime > 0.9 && !_rerollTried && RunManager.Instance.Stats.Rerolls > 0 && _rng.Randf() < 0.5f:
                _rerollTried = true;
                GD.Print("[AutoPilot] reroll");
                Input.ParseInputEvent(new InputEventKey { Keycode = Key.R, Pressed = true });
                _stateTime = 0.2;
                break;

            case "popup" when _stateTime > 0.9:
                _rerollTried = false;
                var cards = main.Game.Popup.Cards;
                int pick = _rng.RandiRange(0, cards.Count - 1);
                if (Mouse)
                {
                    Click(cards[pick].GetGlobalRect().GetCenter());
                }
                else
                {
                    main.Game.Popup.ChooseIndex(pick);
                }
                _stateTime = -10; // wait for the next state change
                break;

            case "placing" when _stateTime > 1.2:
                var board = main.Game.Board;
                var spot = new Vector2(_rng.RandfRange(board.AimRangeMin - 150f, board.AimRangeMax + 150f), _rng.RandfRange(330f, 650f));
                if (Mouse)
                {
                    Click(board.ToGlobal(spot));
                }
                else
                {
                    main.Game.Placer.ConfirmAt(spot);
                }
                _stateTime = -10;
                break;

            case "paused" when _stateTime > 2.0:
                if (Mouse)
                {
                    Click(main.Game.PauseMenu.ResumeButton.GetGlobalRect().GetCenter());
                }
                else
                {
                    Input.ParseInputEvent(new InputEventKey { Keycode = Key.Escape, Pressed = true });
                }
                _stateTime = -10;
                break;

            case "gameover" when _stateTime > 2.5:
                var run = RunManager.Instance;
                string summary = $"run {_runsDone + 1}: {run.SelectedCharacter.Id} reached palier {run.PalierIndex + 1}, " +
                                 $"level {run.Level}, malus {run.MalusLevel}, total {run.Record.TotalScore:0}, " +
                                 $"best hit {run.Record.BestHitPayout:0}, upgrades {run.Record.UpgradesInOrder.Count}, cocktails {run.ActiveModifiers.Count}";
                GD.Print($"[AutoPilot] {summary}");
                _summaries.Add(summary);
                _runsDone++;
                if (_runsDone >= _runsWanted)
                {
                    Finish("all runs done");
                    return;
                }
                if (Mouse)
                {
                    Click(main.Game.GameOverOverlay.RetryButton.GetGlobalRect().GetCenter());
                    _stateTime = -10;
                }
                else
                {
                    main.StartGame(PickCharacter());
                }
                break;

            case "playing":
                PlayStep(main.Game, real);
                break;
        }
    }

    private bool _jumped;

    private void PlayStep(GameScreen game, double real)
    {
        if (!_jumped)
        {
            _jumped = true;
            if (_args.TryGetValue("give", out var give))
            {
                foreach (var id in give.Split(','))
                {
                    var option = Array.Find(UpgradeCatalog.All, o => o.Id == id);
                    if (option != null)
                    {
                        game.EnqueueDebugUpgrade(option);
                    }
                }
            }
            if (_args.TryGetValue("rows", out var rows))
            {
                RunManager.Instance.DebugModifyStats(s => s.RowCount = int.Parse(rows));
            }
            if (_args.TryGetValue("jump", out var jump))
            {
                RunManager.Instance.DebugJumpToPalier(int.Parse(jump) - 1);
                return;
            }
        }
        if (_pauseAt > 0 && !_pauseDone && _time >= _pauseAt)
        {
            _pauseDone = true;
            Input.ParseInputEvent(new InputEventKey { Keycode = Key.Escape, Pressed = true });
            return;
        }
        if (_args.ContainsKey("fast") && Engine.TimeScale < 1.5)
        {
            game.ToggleSpeed();
        }

        _dropTimer -= real;
        var run = RunManager.Instance;
        if (_dropTimer > 0 || !run.IsPlaying || run.BallsRemaining <= 0)
        {
            return;
        }
        _dropTimer = 0.45 + _rng.Randf() * 0.4;

        float x = _rng.RandfRange(game.Board.AimRangeMin, game.Board.AimRangeMax);
        if (Mouse)
        {
            var target = new Vector2(x, 480f);
            MoveMouse(target);
            Click(target);
        }
        else
        {
            game.Board.AimAtLocalX(x);
            game.Drop();
        }
    }

    private bool Mouse => _args.ContainsKey("mouse");

    private Vector2 ToWindow(Vector2 viewportPosition) => GetViewport().GetScreenTransform() * viewportPosition;

    private void MoveMouse(Vector2 viewportPosition)
    {
        var p = ToWindow(viewportPosition);
        Input.ParseInputEvent(new InputEventMouseMotion { Position = p, GlobalPosition = p });
    }

    private void Click(Vector2 viewportPosition)
    {
        var p = ToWindow(viewportPosition);
        MoveMouse(viewportPosition);
        Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = p, GlobalPosition = p });
        Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false, Position = p, GlobalPosition = p });
        GD.Print($"[AutoPilot] click {viewportPosition}");
    }

    private CharacterDef PickCharacter()
    {
        if (_args.TryGetValue("char", out var id))
        {
            return Characters.ById(id);
        }
        return Characters.All[_rng.RandiRange(0, Characters.All.Count - 1)];
    }

    private void Screenshot()
    {
        var image = GetViewport().GetTexture().GetImage();
        string path = $"{_shotDir}/shot_{_shotIndex++:000}_{_lastState}.png";
        image.SavePng(path);
    }

    private void Finish(string reason)
    {
        GD.Print($"[AutoPilot] finished ({reason}) after {_time:0.0}s");
        foreach (var s in _summaries)
        {
            GD.Print($"[AutoPilot] SUMMARY {s}");
        }
        if (_shotDir != null)
        {
            Screenshot();
        }
        GetTree().Quit();
    }
}
