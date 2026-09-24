using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Plinko;

// Debug-only bot for the incremental version, enabled with `-- --autopilot`. It plays
// greedily (drops, buys the most efficient ball or cheapest upgrade, places portals,
// prestiges, spends jetons) and logs the economy, to smoke-test and balance the game.
// It never writes the player's save.
//
//   --minutes=<n>          stop after n minutes of game time (default 10)
//   --shots=<dir>          save screenshots there (windowed runs only)
//   --shot-every=<sec>     screenshot interval in real seconds (default 4)
//   --grant=<coins>,<jetons>  start with extra coins / jetons
//   --tabs                 cycle through the shop tabs (for screenshots)
//   --skills-at=<sec>      open the skill tree once at that time (for screenshots)
//   --no-prestige          never prestige
//   --aim=<0..1>           always aim at that point of the aim range (0 = left, 1 = right)
//   --prestige-at=<sec>    force one prestige (hardest unlocked shoes) at that real time
public partial class AutoPilot : Node
{
    private readonly Dictionary<string, string> _args = new();
    private double _gameTime;
    private double _realTime;
    private double _nextShot = 1.0;
    private double _shotEvery = 4;
    private int _shotIndex;
    private string _shotDir;
    private double _minutes = 10;
    private double _buyTimer;
    private double _dropTimer;
    private double _aimTimer;
    private double _logTimer;
    private double _tabTimer;
    private double _placeTimer;
    private double _runStart;
    private int _tab;
    private double _skillsAt = -1;
    private double _prestigeAt = -1;
    private bool _skillsShown;
    private double _skillsOpenedAt;
    private readonly RandomNumberGenerator _rng = new();

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        SaveData.Disabled = true;
        foreach (var arg in OS.GetCmdlineUserArgs())
        {
            var parts = arg.TrimStart('-').Split('=', 2);
            _args[parts[0]] = parts.Length > 1 ? parts[1] : "true";
        }
        _minutes = Parse("minutes", 10);
        _shotEvery = Parse("shot-every", 4);
        _skillsAt = Parse("skills-at", -1);
        _prestigeAt = Parse("prestige-at", -1);
        if (_args.TryGetValue("shots", out var dir) && DisplayServer.GetName() != "headless")
        {
            _shotDir = dir;
            DirAccess.MakeDirRecursiveAbsolute(_shotDir);
        }
        if (_args.TryGetValue("grant", out var grant))
        {
            var g = grant.Split(',');
            IdleManager.Instance.DebugGrant(double.Parse(g[0], CultureInfo.InvariantCulture), g.Length > 1 ? int.Parse(g[1]) : 0);
        }
        GD.Print($"[AutoPilot] idle bot for {_minutes} game minutes");
    }

    private double Parse(string key, double fallback) =>
        _args.TryGetValue(key, out var v) ? double.Parse(v, CultureInfo.InvariantCulture) : fallback;

    public override void _Process(double delta)
    {
        double real = delta / Math.Max(0.01, Engine.TimeScale);
        _realTime += real;
        if (!GetTree().Paused) _gameTime += delta;

        if (_shotDir != null && _realTime >= _nextShot)
        {
            _nextShot = _realTime + _shotEvery;
            Screenshot();
        }
        if (_gameTime > _minutes * 60.0)
        {
            Log("final");
            var counts = Main.Instance?.Game?.Board?.LandingCounts;
            if (counts != null)
            {
                GD.Print($"[AutoPilot] landings per slot: {string.Join(" ", System.Linq.Enumerable.Take(counts, IdleManager.SlotCount))}");
            }
            GetTree().Quit();
            return;
        }

        var main = Main.Instance;
        if (main == null || main.IsTransitioning) return;
        if (main.Title != null)
        {
            if (_realTime > 1.5) main.StartGame();
            return;
        }
        var game = main.Game;
        if (game == null) return;

        if (game.IsPlacing)
        {
            _placeTimer += real;
            if (_placeTimer > 0.8)
            {
                _placeTimer = 0;
                var b = game.Board;
                game.Placer.ConfirmAt(new Vector2(_rng.RandfRange(b.AimRangeMin - 120f, b.AimRangeMax + 120f), _rng.RandfRange(350f, 650f)));
            }
            return;
        }

        if (game.SkillTree != null)
        {
            BuySkills(game.SkillTree);
            if (_realTime - _skillsOpenedAt > 2.5) game.SkillTree.Close();
            return;
        }
        if (_skillsAt > 0 && !_skillsShown && _realTime > _skillsAt)
        {
            _skillsShown = true;
            OpenSkills(game);
            return;
        }

        var idle = IdleManager.Instance;

        _aimTimer -= delta;
        if (_aimTimer <= 0)
        {
            _aimTimer = 4.0;
            // Aim off-centre sometimes: the edges pay more.
            float t = _args.ContainsKey("aim") ? (float)Parse("aim", 0.5) : _rng.Randf();
            game.Board.AimAtLocalX(Mathf.Lerp(game.Board.AimRangeMin, game.Board.AimRangeMax, t));
        }

        if (!idle.HasAutoDropper)
        {
            _dropTimer -= delta;
            if (_dropTimer <= 0)
            {
                _dropTimer = 0.15;
                game.Board.ManualDrop();
            }
        }

        _buyTimer -= delta;
        if (_buyTimer <= 0)
        {
            _buyTimer = 0.5;
            // Unlock the next ball tier when it's cheap relative to the bank, then upgrades,
            // then restock (manual restock until the auto-restock upgrade is bought).
            if (idle.TiersUnlocked < BallTiers.All.Length && idle.TierUnlockCost(idle.TiersUnlocked) < idle.Coins * 0.6)
            {
                idle.UnlockTier(idle.TiersUnlocked);
            }
            for (int i = 0; i < 6 && idle.BuyCheapestUpgrade(); i++) { }
            if (!idle.IsMaxed(IdleUpgrade.Portal) && idle.UpgradeCost(IdleUpgrade.Portal) < idle.Coins * 0.5)
            {
                idle.BuyUpgrade(IdleUpgrade.Portal);
            }
            if (!idle.HasAutoRestock)
            {
                idle.Restock();
            }
        }

        if (_args.ContainsKey("tabs"))
        {
            _tabTimer -= real;
            if (_tabTimer <= 0)
            {
                _tabTimer = _shotEvery;
                game.Shop.SelectTabIndex(_tab++ % 4);
            }
        }

        _logTimer -= delta;
        if (_logTimer <= 0)
        {
            _logTimer = 30;
            Log("tick");
        }

        // Prestige once the jetons on offer are worth it and the run has had time to grow.
        int gain = idle.JetonsForPrestige;
        if (_prestigeAt > 0 && _realTime > _prestigeAt && gain >= 1)
        {
            _prestigeAt = -1;
            game.RequestPrestige(Characters.All[idle.UnlockedShoes - 1]);
            return;
        }
        // Typical player heuristic: prestige once it at least doubles your jetons.
        if (!_args.ContainsKey("no-prestige") && gain >= Math.Max(4, idle.JetonsEarnedTotal) && _gameTime - _runStart > 300)
        {
            var shoe = Characters.All[idle.UnlockedShoes - 1];
            Log($"prestige +{gain} -> {shoe.Id}");
            _runStart = _gameTime;
            game.RequestPrestige(shoe);
        }
        else if (idle.Jetons > 0 && _gameTime - _runStart < 5 && _gameTime > 10)
        {
            OpenSkills(game);
        }
    }

    private void OpenSkills(IdleGameScreen game)
    {
        game.OpenSkillTreeOverlay();
        _skillsOpenedAt = _realTime;
    }

    private void BuySkills(SkillTreeOverlay tree)
    {
        var affordable = new List<SkillNodeCard>();
        foreach (var card in tree.Cards)
        {
            if (IdleManager.Instance.CanBuySkill(card.Node)) affordable.Add(card);
        }
        if (affordable.Count > 0)
        {
            tree.Buy(affordable[_rng.RandiRange(0, affordable.Count - 1)]);
        }
    }

    private void Log(string tag)
    {
        var idle = IdleManager.Instance;
        GD.Print($"[AutoPilot] {tag} t={_gameTime / 60.0:0.0}min coins={Big.Format(idle.Coins)} income={Big.Format(idle.IncomePerSecond)}/s " +
                 $"run={Big.Format(idle.RunEarned)} stock={Big.Format(idle.TotalStock)} tiers={idle.TiersUnlocked} cadence={idle.Cadence:0.0} upg={string.Join("/", idle.UpgradeLevels)} " +
                 $"jetons={idle.Jetons}/{idle.JetonsEarnedTotal} shoe={idle.ShoeId} unlocked={idle.UnlockedShoes}");
    }

    private void Screenshot()
    {
        var image = GetViewport().GetTexture().GetImage();
        image.SavePng($"{_shotDir}/shot_{_shotIndex++:000}.png");
    }
}
