using Godot;
using System;
using System.Collections.Generic;

namespace Plinko;

// The whole incremental economy: coins, balls owned, upgrades, prestige (shoes + jetons),
// skill tree, golden-chest frenzy, auto-buy, offline earnings and saving. Knows nothing
// about nodes on screen; the board and UI read from it and listen to its events.
public partial class IdleManager : Node
{
    public static IdleManager Instance { get; private set; }

    public event Action Changed;                       // something purchasable changed
    public event Action BoardLayoutChanged;            // rows / slot multipliers changed
    public event Action<PlaceableKind> PlacementRequested;
    public event Action<string, string, Color> Announce;
    public event Action<AchievementDef> AchievementUnlocked;

    public const int StartingBalls = 25;
    public const int SlotCount = 13;
    public const double StartingCoins = 50;
    private const double AutosaveSeconds = 15.0;
    private const double BaseCadence = 2.0;   // balls per second from the dropper
    private const double JetonScale = 1e6;

    // ---- current run
    public double Coins { get; private set; }
    public double RunEarned { get; private set; }
    // Consumable stock: a ball is spent when it drops and destroyed when it lands.
    public double[] Stock { get; private set; } = new double[BallTiers.All.Length];
    public int TiersUnlocked { get; private set; } = 1;
    public int[] UpgradeLevels { get; private set; } = new int[Upgrades.All.Length];
    public List<Vector2I> PortalCells { get; } = new();
    public int PendingPortals { get; private set; }

    // ---- permanent
    public double LifetimeEarned { get; private set; }
    public int Jetons { get; private set; }
    public int JetonsEarnedTotal { get; private set; }
    public int Prestiges { get; private set; }
    public string ShoeId { get; private set; } = Characters.Classic.Id;
    public int UnlockedShoes { get; private set; } = 1;
    private readonly Dictionary<string, int> _skills = new();
    private readonly HashSet<string> _achievements = new();
    private double _achievementTimer;
    private double _rescueTimer;
    public bool AutoBuyBalls { get; set; } = true;    // auto-restock toggle
    public double LifetimeBallsDropped { get; private set; }
    public bool AutoBuyUpgrades { get; set; } = true;

    // ---- transient
    public double FrenzyTimeLeft { get; private set; }
    public double FrenzyMultiplier { get; private set; } = 1.0;
    public double IncomePerSecond { get; private set; }
    public double OfflineGain { get; private set; }
    public double OfflineSeconds { get; private set; }

    // 20 one-second buckets: long enough to smooth out the swings from crits and jackpots.
    private readonly double[] _buckets = new double[20];
    private int _bucket;
    private double _bucketTime;
    private double _autosaveTimer;
    private double _autoBuyTimer;
    private bool _loaded;

    public CharacterDef Shoe => Characters.ById(ShoeId);

    public override void _Ready()
    {
        Instance = this;
        Load();
    }

    public override void _Notification(int what)
    {
        if (what == NotificationWMCloseRequest || what == NotificationExitTree)
        {
            if (_loaded)
            {
                Save();
            }
        }
    }

    // ================================================================ derived values

    public int SkillLevel(string id) => _skills.GetValueOrDefault(id);
    public int Level(IdleUpgrade id) => UpgradeLevels[(int)id];

    public int Rows => Upgrades.BaseRows + Level(IdleUpgrade.Rows);
    public bool HasAutoDropper => Level(IdleUpgrade.AutoDropper) > 0;
    public bool HasAutoRestock => Level(IdleUpgrade.AutoRestock) > 0;
    public double TotalStock { get { double t = 0; foreach (double b in Stock) t += b; return t; } }

    // Balls per second released by the auto-dropper.
    public double Cadence => HasAutoDropper
        ? BaseCadence * Math.Pow(1.25, Level(IdleUpgrade.Cadence)) * (1.0 + 0.15 * SkillLevel("a_auto")) * Shoe.CadenceMultiplier
        : 0.0;

    public double CritChance => Math.Min(0.6, 0.03 * Level(IdleUpgrade.Critical));
    public double CritMultiplier => 10 + 5 * SkillLevel("f_crit");
    public double PegFraction => 0.03 * Level(IdleUpgrade.GoldenPegs) * Math.Pow(2, SkillLevel("f_pegs"));
    public double SlotBoost => Math.Pow(1.25, Level(IdleUpgrade.Slots)) * Shoe.SlotMultiplier;
    public double EdgeBoost => SkillLevel("f_edges") > 0 ? 3.0 : 1.0;

    public double CostMultiplier => Shoe.CostMultiplier * (1.0 - 0.05 * SkillLevel("e_cost"));

    // Permanent bonuses: every jeton ever earned (+1%), every pair of shoes unlocked (+50%).
    public double PrestigeBonus => (1.0 + 0.01 * JetonsEarnedTotal) * (1.0 + 0.5 * (UnlockedShoes - 1));

    public bool HasAchievement(string id) => _achievements.Contains(id);
    public int AchievementCount => _achievements.Count;
    public double AchievementBonus => 1.0 + Achievements.BonusPerAchievement * _achievements.Count;

    public double GlobalMultiplier =>
        Math.Pow(1.2, Level(IdleUpgrade.Value)) * (1.0 + 0.25 * SkillLevel("f_income")) * PrestigeBonus * AchievementBonus * FrenzyMultiplier;

    // Base multipliers of a board with `rows` rows: x1 in the middle, growing
    // quadratically-exponentially toward the edges; more rows = much bigger edges.
    public static double[] BaseSlotMultipliers(int rows)
    {
        int n = SlotCount;
        double c = (n - 1) / 2.0;
        // The middle pays less than a ball costs (x0.7): aiming off-centre is what turns a
        // profit, and the edges are the jackpots.
        const double centre = 0.7;
        double edgeMax = 25.0 * Math.Pow(rows / 8.0, 2.0);
        var result = new double[n];
        for (int i = 0; i < n; i++)
        {
            double d = Math.Abs(i - c) / c;
            double m = centre * Math.Pow(edgeMax / centre, d * d);
            result[i] = m < 10 ? Math.Round(m * 10) / 10 : Math.Round(m);
        }
        return result;
    }

    public double[] SlotMultipliers()
    {
        var baseValues = BaseSlotMultipliers(Rows);
        var result = new double[baseValues.Length];
        for (int i = 0; i < baseValues.Length; i++)
        {
            bool edge = i == 0 || i == baseValues.Length - 1;
            result[i] = baseValues[i] * SlotBoost * (edge ? EdgeBoost : 1.0);
        }
        return result;
    }

    // ================================================================ costs & purchases

    public double BallPrice(int tier) =>
        BallTiers.All[tier].Price * CostMultiplier * (1.0 - 0.1 * SkillLevel("a_balls"));

    public double MaxAffordableBalls(int tier) => Math.Floor(Coins / BallPrice(tier));

    public bool BuyBalls(int tier, double amount)
    {
        amount = Math.Floor(amount);
        if (amount <= 0 || tier >= TiersUnlocked) return false;
        double cost = BallPrice(tier) * amount;
        if (cost > Coins) return false;
        Coins -= cost;
        Stock[tier] += amount;
        // Income is shown net of ball purchases: that's the real profit of the machine.
        _buckets[_bucket] -= cost;
        Changed?.Invoke();
        return true;
    }

    public double TierUnlockCost(int tier) => BallTiers.All[tier].UnlockCost * CostMultiplier;

    public bool UnlockTier(int tier)
    {
        if (tier != TiersUnlocked || tier >= BallTiers.All.Length) return false;
        double cost = TierUnlockCost(tier);
        if (cost > Coins) return false;
        Coins -= cost;
        TiersUnlocked++;
        Changed?.Invoke();
        return true;
    }

    // Takes up to `count` balls of the best tier in stock for one drop.
    public (int tier, double taken) TakeForDrop(double count)
    {
        for (int t = TiersUnlocked - 1; t >= 0; t--)
        {
            if (Stock[t] >= 1)
            {
                double taken = Math.Min(Math.Floor(Stock[t]), Math.Max(1, Math.Floor(count)));
                Stock[t] -= taken;
                LifetimeBallsDropped += taken;
                return (t, taken);
            }
        }
        return (-1, 0);
    }

    public bool IsMaxed(IdleUpgrade id) => Level(id) >= Upgrades.Get(id).MaxLevel;

    public double UpgradeCost(IdleUpgrade id)
    {
        var def = Upgrades.Get(id);
        return def.BaseCost * Math.Pow(def.Growth, Level(id)) * CostMultiplier;
    }

    public bool BuyUpgrade(IdleUpgrade id)
    {
        if (IsMaxed(id)) return false;
        double cost = UpgradeCost(id);
        if (cost > Coins) return false;
        Coins -= cost;
        UpgradeLevels[(int)id]++;
        if (id is IdleUpgrade.Rows or IdleUpgrade.Slots)
        {
            BoardLayoutChanged?.Invoke();
        }
        if (id == IdleUpgrade.Portal)
        {
            PendingPortals++;
            PlacementRequested?.Invoke(PlaceableKind.Portal);
        }
        Changed?.Invoke();
        return true;
    }

    public void CommitPortal(Vector2I cell)
    {
        PortalCells.Add(cell);
        PendingPortals = Math.Max(0, PendingPortals - 1);
        Save();
    }

    // ================================================================ earning

    // One-off windfalls (offline earnings, chest lump sums) don't count toward income per
    // second, which would otherwise spike and feed back into the next offline estimate.
    private void Earn(double amount, bool countsAsIncome = true)
    {
        if (amount <= 0 || double.IsNaN(amount) || double.IsInfinity(amount)) return;
        Coins += amount;
        RunEarned += amount;
        LifetimeEarned += amount;
        if (countsAsIncome)
        {
            _buckets[_bucket] += amount;
        }
        CheckShoeUnlocks();
    }

    // A ball landed: returns the payout and whether it was a critical hit.
    public (double payout, bool crit) Land(int tier, double stack, double slotMultiplier)
    {
        bool crit = GD.Randf() < CritChance;
        double payout = BallTiers.All[tier].Value * stack * slotMultiplier * GlobalMultiplier * (crit ? CritMultiplier : 1.0);
        Earn(payout);
        return (payout, crit);
    }

    public double PegHit(int tier, double stack)
    {
        if (PegFraction <= 0) return 0;
        double payout = BallTiers.All[tier].Value * stack * PegFraction * GlobalMultiplier;
        Earn(payout);
        return payout;
    }

    // Golden chest: either a frenzy or a lump sum (Cookie Clicker's "Lucky!" formula).
    public (string title, string detail) OpenGoldenChest()
    {
        double generosity = 1.0 + 0.5 * SkillLevel("e_chest");
        if (GD.Randf() < 0.5f)
        {
            FrenzyMultiplier = 7.0;
            FrenzyTimeLeft = 30.0 * generosity;
            return ("FRÉNÉSIE !", $"Gains x7 pendant {FrenzyTimeLeft:0} secondes");
        }
        double lump = (Math.Min(Coins * 0.15, IncomePerSecond * 900.0) + 25.0) * generosity;
        Earn(lump, countsAsIncome: false);
        return ("JACKPOT DU COFFRE !", $"+{Big.Format(lump)} pièces");
    }

    public double GoldenChestInterval => (70.0 + GD.Randf() * 80.0) * Math.Pow(0.7, SkillLevel("e_chest"));

    // ================================================================ prestige

    // Cube root (like Cookie Clicker's prestige), so late-game runs don't flood the tree.
    public int JetonsForPrestige => (int)Math.Floor(Math.Cbrt(RunEarned / JetonScale) * Shoe.JetonMultiplier);

    // Earnings needed (this run, current shoes) for the next jeton.
    public double NextJetonAt
    {
        get
        {
            int next = JetonsForPrestige + 1;
            double ratio = next / Shoe.JetonMultiplier;
            return ratio * ratio * ratio * JetonScale;
        }
    }

    public bool IsShoeUnlocked(int index) => index < UnlockedShoes;
    public int ShoeIndex => Characters.All.FindIndex(c => c.Id == ShoeId);

    private void CheckShoeUnlocks()
    {
        if (UnlockedShoes >= Characters.All.Count) return;
        // The next pair needs its threshold earned in a single run with the pair just below
        // it (or any harder one).
        var next = Characters.All[UnlockedShoes];
        if (ShoeIndex >= UnlockedShoes - 1 && RunEarned >= next.UnlockRunEarnings)
        {
            UnlockedShoes++;
            Announce?.Invoke("NOUVELLE PAIRE !", $"{next.Name} débloquées : plus dures, mais bien plus de jetons.", next.ShoeColor);
            Save();
        }
    }

    public void Prestige(CharacterDef shoe)
    {
        int gain = JetonsForPrestige;
        Jetons += gain;
        JetonsEarnedTotal += gain;
        Prestiges++;
        ShoeId = shoe.Id;
        ResetRun();
        GD.Print($"[Idle] prestige #{Prestiges}: +{gain} jetons, now wearing {shoe.Name}");
        Save();
    }

    private void ResetRun()
    {
        double[] startCoins = { StartingCoins, 500, 50_000, 5_000_000 };
        Coins = startCoins[Math.Min(3, SkillLevel("e_start"))];
        RunEarned = 0;
        Stock = new double[BallTiers.All.Length];
        Stock[0] = StartingBalls;
        TiersUnlocked = 1;
        UpgradeLevels = new int[Upgrades.All.Length];
        if (SkillLevel("a_auto") > 0)
        {
            UpgradeLevels[(int)IdleUpgrade.AutoDropper] = 1;
            UpgradeLevels[(int)IdleUpgrade.AutoRestock] = 1;
        }
        PortalCells.Clear();
        PendingPortals = SkillLevel("e_portal") > 0 ? 1 : 0;
        FrenzyTimeLeft = 0;
        FrenzyMultiplier = 1.0;
        Array.Clear(_buckets);
        IncomePerSecond = 0;
    }

    // ================================================================ skills

    public int SkillCost(SkillNode node)
    {
        int level = SkillLevel(node.Id);
        return level >= node.MaxLevel ? -1 : node.Costs[level];
    }

    public bool IsSkillUnlocked(SkillNode node) => node.RequiresId == null || SkillLevel(node.RequiresId) > 0;

    public bool CanBuySkill(SkillNode node)
    {
        int cost = SkillCost(node);
        return cost >= 0 && IsSkillUnlocked(node) && Jetons >= cost;
    }

    public bool BuySkill(SkillNode node)
    {
        if (!CanBuySkill(node)) return false;
        Jetons -= SkillCost(node);
        _skills[node.Id] = SkillLevel(node.Id) + 1;
        if (node.Id == "a_auto")
        {
            UpgradeLevels[(int)IdleUpgrade.AutoDropper] = 1;
            UpgradeLevels[(int)IdleUpgrade.AutoRestock] = 1;
        }
        if (node.Id == "f_edges")
        {
            BoardLayoutChanged?.Invoke();
        }
        Changed?.Invoke();
        Save();
        return true;
    }

    public int SpentJetons()
    {
        int spent = 0;
        foreach (var node in SkillTree.Nodes)
        {
            for (int i = 0; i < SkillLevel(node.Id); i++) spent += node.Costs[i];
        }
        return spent;
    }

    public void ResetSkills()
    {
        Jetons += SpentJetons();
        _skills.Clear();
        BoardLayoutChanged?.Invoke();
        Changed?.Invoke();
        Save();
    }

    // ================================================================ loop

    public override void _Process(double delta)
    {
        if (!_loaded) return;

        _bucketTime += delta;
        if (_bucketTime >= 1.0)
        {
            _bucketTime -= 1.0;
            double sum = 0;
            foreach (double b in _buckets) sum += b;
            IncomePerSecond = sum / _buckets.Length;
            _bucket = (_bucket + 1) % _buckets.Length;
            _buckets[_bucket] = 0;
        }

        if (FrenzyTimeLeft > 0)
        {
            FrenzyTimeLeft -= delta;
            if (FrenzyTimeLeft <= 0)
            {
                FrenzyTimeLeft = 0;
                FrenzyMultiplier = 1.0;
            }
        }

        // Anti soft-lock: broke and out of balls? A free basic ball every second.
        if (TotalStock < 1 && Coins < BallPrice(0))
        {
            _rescueTimer += delta;
            if (_rescueTimer >= 1.0)
            {
                _rescueTimer = 0;
                Stock[0] += 1;
            }
        }

        _achievementTimer += delta;
        if (_achievementTimer >= 0.5)
        {
            _achievementTimer = 0;
            CheckAchievements();
        }

        _autoBuyTimer += delta;
        if (_autoBuyTimer >= 0.3)
        {
            _autoBuyTimer = 0;
            AutoBuy();
        }

        _autosaveTimer += delta / Math.Max(0.01, Engine.TimeScale);
        if (_autosaveTimer >= AutosaveSeconds)
        {
            _autosaveTimer = 0;
            Save();
        }
    }

    private void CheckAchievements()
    {
        foreach (var achievement in Achievements.All)
        {
            if (!_achievements.Contains(achievement.Id) && achievement.Condition(this))
            {
                _achievements.Add(achievement.Id);
                GD.Print($"[Idle] achievement {achievement.Id}");
                AchievementUnlocked?.Invoke(achievement);
                Changed?.Invoke();
            }
        }
    }

    private void AutoBuy()
    {
        bool bought = false;
        if (HasAutoRestock && AutoBuyBalls)
        {
            bought |= Restock();
        }
        if (SkillLevel("a_upgrades") > 0 && AutoBuyUpgrades)
        {
            for (int i = 0; i < 5 && BuyCheapestUpgrade(); i++)
            {
                bought = true;
            }
        }
        if (bought)
        {
            Changed?.Invoke();
        }
    }

    // Keeps about 20 seconds of dropping in stock, buying the best tier it can afford
    // (falling back to cheaper tiers when the best one is out of reach).
    public bool Restock()
    {
        double target = Math.Max(30, Cadence * 20);
        if (TotalStock >= target) return false;
        for (int t = TiersUnlocked - 1; t >= 0; t--)
        {
            // Never spend more than half the bank on stock: upgrades need coins too.
            double affordable = Math.Floor(Coins * 0.5 / BallPrice(t));
            double amount = Math.Min(affordable, target - TotalStock);
            if (amount >= 1 && BuyBalls(t, amount))
            {
                return true;
            }
        }
        // Broke and out of balls: spend whatever is left on basic balls so the game never stalls.
        if (TotalStock < 1 && Coins >= BallPrice(0))
        {
            return BuyBalls(0, MaxAffordableBalls(0));
        }
        return false;
    }

    public bool BuyCheapestUpgrade()
    {
        UpgradeDef cheapest = null;
        double cheapestCost = double.MaxValue;
        foreach (var def in Upgrades.All)
        {
            if (!def.AutoBuyable || IsMaxed(def.Id)) continue;
            double cost = UpgradeCost(def.Id);
            if (cost < cheapestCost)
            {
                cheapestCost = cost;
                cheapest = def;
            }
        }
        return cheapest != null && cheapestCost <= Coins && BuyUpgrade(cheapest.Id);
    }

    // ================================================================ save / load

    public void Save()
    {
        var f = SaveData.File;
        f.SetValue("run", "coins", Coins);
        f.SetValue("run", "earned", RunEarned);
        f.SetValue("run", "stock", string.Join(",", Array.ConvertAll(Stock, v => v.ToString("R", System.Globalization.CultureInfo.InvariantCulture))));
        f.SetValue("run", "tiers", TiersUnlocked);
        f.SetValue("run", "upgrades", string.Join(",", UpgradeLevels));
        var portals = new List<string>();
        foreach (var c in PortalCells) portals.Add($"{c.X}:{c.Y}");
        f.SetValue("run", "portals", string.Join(",", portals));
        f.SetValue("run", "pending_portals", PendingPortals);

        f.SetValue("meta", "lifetime", LifetimeEarned);
        f.SetValue("meta", "balls_dropped", LifetimeBallsDropped);
        f.SetValue("meta", "jetons", Jetons);
        f.SetValue("meta", "jetons_total", JetonsEarnedTotal);
        f.SetValue("meta", "prestiges", Prestiges);
        f.SetValue("meta", "shoe", ShoeId);
        f.SetValue("meta", "unlocked_shoes", UnlockedShoes);
        f.SetValue("meta", "autobuy_balls", AutoBuyBalls);
        f.SetValue("meta", "autobuy_upgrades", AutoBuyUpgrades);
        f.SetValue("meta", "last_income", IncomePerSecond);
        f.SetValue("meta", "last_save", Time.GetUnixTimeFromSystem());
        f.SetValue("meta", "achievements", string.Join(",", _achievements));

        foreach (var node in SkillTree.Nodes)
        {
            f.SetValue("skills", node.Id, SkillLevel(node.Id));
        }
        SaveData.Save();
    }

    private void Load()
    {
        var f = SaveData.File;
        bool fresh = !f.HasSection("run");
        if (fresh)
        {
            ResetRun();
            _loaded = true;
            return;
        }

        foreach (var node in SkillTree.Nodes)
        {
            int level = (int)f.GetValue("skills", node.Id, 0);
            if (level > 0) _skills[node.Id] = Math.Min(level, node.MaxLevel);
        }

        Coins = (double)f.GetValue("run", "coins", 0.0);
        RunEarned = (double)f.GetValue("run", "earned", 0.0);
        var stock = ((string)f.GetValue("run", "stock", "")).Split(',', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < Stock.Length && i < stock.Length; i++)
        {
            double.TryParse(stock[i], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out Stock[i]);
        }
        TiersUnlocked = Math.Clamp((int)f.GetValue("run", "tiers", 1), 1, BallTiers.All.Length);
        ParseInts((string)f.GetValue("run", "upgrades", ""), UpgradeLevels);
        foreach (var part in ((string)f.GetValue("run", "portals", "")).Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var xy = part.Split(':');
            if (xy.Length == 2 && int.TryParse(xy[0], out int x) && int.TryParse(xy[1], out int y))
            {
                PortalCells.Add(new Vector2I(x, y));
            }
        }
        PendingPortals = (int)f.GetValue("run", "pending_portals", 0);

        LifetimeEarned = (double)f.GetValue("meta", "lifetime", 0.0);
        LifetimeBallsDropped = (double)f.GetValue("meta", "balls_dropped", 0.0);
        Jetons = (int)f.GetValue("meta", "jetons", 0);
        JetonsEarnedTotal = (int)f.GetValue("meta", "jetons_total", 0);
        Prestiges = (int)f.GetValue("meta", "prestiges", 0);
        ShoeId = (string)f.GetValue("meta", "shoe", Characters.Classic.Id);
        UnlockedShoes = Math.Clamp((int)f.GetValue("meta", "unlocked_shoes", 1), 1, Characters.All.Count);
        AutoBuyBalls = (bool)f.GetValue("meta", "autobuy_balls", true);
        AutoBuyUpgrades = (bool)f.GetValue("meta", "autobuy_upgrades", true);
        foreach (var id in ((string)f.GetValue("meta", "achievements", "")).Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            _achievements.Add(id);
        }
        _loaded = true;

        // Offline earnings: a share of the last known income rate, capped in duration.
        double last = (double)f.GetValue("meta", "last_save", 0.0);
        double rate = (double)f.GetValue("meta", "last_income", 0.0);
        double away = Time.GetUnixTimeFromSystem() - last;
        if (last > 0 && away > 60 && rate > 0)
        {
            int level = SkillLevel("a_offline");
            double share = 0.25 + 0.25 * level;
            double cap = (4 + 8 * level) * 3600.0;
            OfflineSeconds = Math.Min(away, cap);
            OfflineGain = rate * OfflineSeconds * share;
            Earn(OfflineGain, countsAsIncome: false);
            IncomePerSecond = rate;
            GD.Print($"[Idle] offline {away:0}s -> +{OfflineGain:0}");
        }
    }

    public void ConsumeOfflineReport()
    {
        OfflineGain = 0;
        OfflineSeconds = 0;
    }

    public void WipeSave()
    {
        SaveData.Wipe();
        _skills.Clear();
        _achievements.Clear();
        LifetimeEarned = 0;
        LifetimeBallsDropped = 0;
        Jetons = 0;
        JetonsEarnedTotal = 0;
        Prestiges = 0;
        ShoeId = Characters.Classic.Id;
        UnlockedShoes = 1;
        AutoBuyBalls = AutoBuyUpgrades = true;
        ResetRun();
        Save();
    }

    // Debug/autopilot helpers.
    public void DebugGrant(double coins, int jetons)
    {
        Earn(coins, countsAsIncome: false);
        Jetons += jetons;
        JetonsEarnedTotal += jetons;
        Changed?.Invoke();
    }

    private static void ParseInts(string text, int[] into)
    {
        var parts = text.Split(',', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < into.Length && i < parts.Length; i++)
        {
            if (int.TryParse(parts[i], out int v)) into[i] = v;
        }
    }
}

// Big-number formatting for incremental amounts: 1.23K, 45.6M, ... then scientific.
public static class Big
{
    private static readonly string[] Suffixes = { "", "K", "M", "B", "T", "Qa", "Qi", "Sx", "Sp", "Oc", "No", "Dc" };

    public static string Format(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value)) return "∞";
        if (value < 0) return "-" + Format(-value);
        if (value < 1000)
        {
            return value < 10 && value != Math.Floor(value) ? value.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) : Math.Floor(value).ToString("0");
        }
        int tier = (int)Math.Floor(Math.Log10(value) / 3);
        if (tier >= Suffixes.Length)
        {
            return value.ToString("0.00e0", System.Globalization.CultureInfo.InvariantCulture);
        }
        double scaled = value / Math.Pow(1000, tier);
        string number = scaled >= 100 ? scaled.ToString("0") : scaled >= 10 ? scaled.ToString("0.0") : scaled.ToString("0.00");
        return number.Replace(',', '.') + Suffixes[tier];
    }
}
