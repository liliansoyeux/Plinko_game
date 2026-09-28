using Godot;
using System;

namespace Plinko;

// Ball tiers of the forge: balls are free and endless (one on the board at a time). Each
// tier levels up from 1 to 10 (x1.25 per level), and at level 10 the next tier can be
// forged: level 1 of a tier (x8) is always worth more than level 10 of the one before.
public class BallTierDef
{
    public int Index;
    public string Name;
    public Color Color;
    public Color Glow;
    public double Value;       // coins per landing, before slot & multipliers
    public double ForgeCost;   // one-time cost to forge this tier
    public int RequiredPrestiges;  // prestiges done before this tier can be forged
}

public static class BallTiers
{
    public static readonly BallTierDef[] All =
    {
        new() { Index = 0, Name = "Bille", Color = new Color(0.98f, 0.93f, 1f), Glow = Pal.Pink, Value = 10, ForgeCost = 0 },
        new() { Index = 1, Name = "Bille d'argent", Color = new Color(0.82f, 0.88f, 0.96f), Glow = new Color(0.6f, 0.8f, 1f), Value = 80, ForgeCost = 20_000 },
        new() { Index = 2, Name = "Bille d'or", Color = new Color(1f, 0.8f, 0.3f), Glow = Pal.Gold, Value = 640, ForgeCost = 8e6 },
        new() { Index = 3, Name = "Bille de diamant", Color = new Color(0.75f, 1f, 1f), Glow = Pal.Cyan, Value = 5_120, ForgeCost = 4e9, RequiredPrestiges = 2 },
        new() { Index = 4, Name = "Bille de rubis", Color = new Color(1f, 0.35f, 0.45f), Glow = Pal.Red, Value = 40_960, ForgeCost = 2e12, RequiredPrestiges = 5 },
        new() { Index = 5, Name = "Bille cosmique", Color = new Color(0.8f, 0.6f, 1f), Glow = Pal.Purple, Value = 327_680, ForgeCost = 1e15, RequiredPrestiges = 9 },
    };
}

public enum IdleUpgrade
{
    AutoDropper,
    Cadence,
    Value,
    Rows,
    GoldenPegs,
    Critical,
    Slots,
    Portal,
    AutoRestock,
    Combo,
    Twin,
    Jackpot,
    Interest,
    ChestHunter,
}

public class UpgradeDef
{
    public IdleUpgrade Id;
    public string Name;
    public Func<int, string> Describe;   // text for the NEXT level
    public UpgradeIcon Icon;
    public double BaseCost;
    public double Growth;
    public int MaxLevel;
    public bool AutoBuyable = true;
    public int RequiredPrestiges;   // prestiges done before it shows up in the shop
    // Retired upgrades stay in the enum (save files store levels by index) but are hidden
    // from the shop and never bought.
    public bool Retired;
}

public static class Upgrades
{
    public const int BaseRows = 8;

    public static readonly UpgradeDef[] All =
    {
        new()
        {
            Id = IdleUpgrade.AutoDropper, Name = "Distributeur automatique", Icon = UpgradeIcon.Gear,
            Describe = _ => "Relance ta bille tout seul dès qu'elle est tombée.",
            BaseCost = 30, Growth = 1, MaxLevel = 1,
        },
        new()
        {
            Id = IdleUpgrade.Cadence, Name = "Gravité", Icon = UpgradeIcon.Clock,
            Describe = l => $"Ta bille tombe et revient plus vite : gravité x{1 + 0.15 * (l + 1):0.##} (niveau {l + 1}).",
            BaseCost = 40, Growth = 2.8, MaxLevel = 25,
        },
        new()
        {
            Id = IdleUpgrade.Value, Name = "Polissage", Icon = UpgradeIcon.Multiplier,
            Describe = l => $"Ta bille rapporte x1,2 (niveau {l + 1}).",
            BaseCost = 100, Growth = 9, MaxLevel = 40, Retired = true,
        },
        new()
        {
            Id = IdleUpgrade.Rows, Name = "+1 Rangée", Icon = UpgradeIcon.Rows,
            Describe = l => $"Les billes s'éparpillent plus et les bords rapportent plus ({BaseRows + l + 1} rangées).",
            BaseCost = 1_000, Growth = 25, MaxLevel = 8, Retired = true,
        },
        new()
        {
            Id = IdleUpgrade.GoldenPegs, Name = "Clous dorés", Icon = UpgradeIcon.Peg,
            Describe = l => $"Chaque clou touché rapporte {(l + 1) * 3}% de la valeur de la bille.",
            BaseCost = 400, Growth = 4, MaxLevel = 25,
        },
        new()
        {
            Id = IdleUpgrade.Critical, Name = "Coup critique", Icon = UpgradeIcon.Star,
            Describe = l => $"{(l + 1) * 3}% de chances qu'une bille rapporte x10.",
            BaseCost = 5_000, Growth = 5, MaxLevel = 15, RequiredPrestiges = 1,
        },
        new()
        {
            Id = IdleUpgrade.Slots, Name = "Cases renforcées", Icon = UpgradeIcon.NarrowSlots,
            Describe = l => $"Tous les multiplicateurs de cases x1,25 (niveau {l + 1}).",
            BaseCost = 50_000, Growth = 11, MaxLevel = 30, RequiredPrestiges = 2,
        },
        new()
        {
            Id = IdleUpgrade.Portal, Name = "Portail dédoubleur", Icon = UpgradeIcon.Portal,
            Describe = l => "Place un portail : chaque bille qui le traverse se dédouble.",
            BaseCost = 1e10, Growth = 200, MaxLevel = 3, AutoBuyable = false, RequiredPrestiges = 8,
        },
        new()
        {
            Id = IdleUpgrade.AutoRestock, Name = "Réapprovisionnement auto", Icon = UpgradeIcon.ExtraBalls,
            Describe = _ => "",
            BaseCost = 150, Growth = 1, MaxLevel = 1, Retired = true,
        },
        new()
        {
            Id = IdleUpgrade.Combo, Name = "Rebonds en chaîne", Icon = UpgradeIcon.Xp,
            Describe = l => $"Chaque clou touché pendant la chute ajoute +{(l + 1) * 4}% au gain de la bille.",
            BaseCost = 150, Growth = 3.6, MaxLevel = 25,
        },
        new()
        {
            Id = IdleUpgrade.Twin, Name = "Bille jumelle", Icon = UpgradeIcon.TwinBall,
            Describe = l => $"{(l + 1) * 5}% de chances qu'une deuxième bille parte avec la tienne.",
            BaseCost = 1e6, Growth = 7, MaxLevel = 10, RequiredPrestiges = 4,
        },
        new()
        {
            Id = IdleUpgrade.Jackpot, Name = "Case jackpot", Icon = UpgradeIcon.Multiplier,
            Describe = l => $"Une case brille : elle rapporte x{IdleManager.JackpotFor(l + 1):0}, puis le jackpot change de case.",
            BaseCost = 20_000, Growth = 5, MaxLevel = 20, RequiredPrestiges = 1,
        },
        new()
        {
            Id = IdleUpgrade.Interest, Name = "Intérêts", Icon = UpgradeIcon.GoldBall,
            Describe = l => $"Toutes les 10 s, +{(l + 1) * 2}% de tes pièces (au plus {(l + 1) * 10} s de gains).",
            BaseCost = 1e8, Growth = 8, MaxLevel = 10, RequiredPrestiges = 6,
        },
        new()
        {
            Id = IdleUpgrade.ChestHunter, Name = "Chasseur de coffres", Icon = UpgradeIcon.Chest,
            Describe = l => $"Le coffre en or apparaît {100 - Math.Round(100 * Math.Pow(0.85, l + 1)):0}% plus souvent.",
            BaseCost = 100_000, Growth = 6, MaxLevel = 8, RequiredPrestiges = 3,
        },
    };

    public static UpgradeDef Get(IdleUpgrade id) => All[(int)id];
}

public enum SkillBranch
{
    Fortune,
    Automation,
    Economy,
}

public class SkillNode
{
    public string Id;
    public SkillBranch Branch;
    public int Tier;
    public string Name;
    public string Description;
    public UpgradeIcon Icon;
    public int[] Costs;
    public string RequiresId;

    public int MaxLevel => Costs.Length;
}

// Permanent upgrades bought with jetons (prestige currency). Effects are read by IdleManager.
public static class SkillTree
{
    public static readonly SkillNode[] Nodes =
    {
        new() { Id = "f_income", Branch = SkillBranch.Fortune, Tier = 0, Name = "Revenus", Icon = UpgradeIcon.Multiplier,
                Description = "Tous les gains +25% par niveau.", Costs = new[] { 2, 4, 8, 15, 25 } },
        new() { Id = "f_crit", Branch = SkillBranch.Fortune, Tier = 1, Name = "Pièces d'or", Icon = UpgradeIcon.GoldBall, RequiresId = "f_income",
                Description = "Les coups critiques rapportent +5x par niveau.", Costs = new[] { 8, 20, 45 } },
        new() { Id = "f_pegs", Branch = SkillBranch.Fortune, Tier = 2, Name = "Clous précieux", Icon = UpgradeIcon.Peg, RequiresId = "f_crit",
                Description = "Gains des clous dorés x2 par niveau.", Costs = new[] { 30, 80 } },
        new() { Id = "f_edges", Branch = SkillBranch.Fortune, Tier = 3, Name = "Bords dorés", Icon = UpgradeIcon.Star, RequiresId = "f_pegs",
                Description = "Les cases extrêmes rapportent x3.", Costs = new[] { 200 } },

        new() { Id = "a_auto", Branch = SkillBranch.Automation, Tier = 0, Name = "Distributeur offert", Icon = UpgradeIcon.Gear,
                Description = "Commence avec le distributeur automatique. Gravité +15% par niveau.", Costs = new[] { 2, 6, 12 } },
        new() { Id = "a_balls", Branch = SkillBranch.Automation, Tier = 1, Name = "Forgeron", Icon = UpgradeIcon.ExtraBalls, RequiresId = "a_auto",
                Description = "Forger la bille suivante coûte 10% moins cher par niveau.", Costs = new[] { 6, 15, 35 } },
        new() { Id = "a_upgrades", Branch = SkillBranch.Automation, Tier = 2, Name = "Intendant", Icon = UpgradeIcon.Xp, RequiresId = "a_balls",
                Description = "Achète automatiquement les améliorations.", Costs = new[] { 35 } },
        new() { Id = "a_offline", Branch = SkillBranch.Automation, Tier = 3, Name = "Gains hors-ligne", Icon = UpgradeIcon.Clock, RequiresId = "a_upgrades",
                Description = "Hors-ligne : +25% des gains et +8 h de durée par niveau.", Costs = new[] { 25, 60, 150 } },

        new() { Id = "e_cost", Branch = SkillBranch.Economy, Tier = 0, Name = "Négociateur", Icon = UpgradeIcon.MultiplierDown,
                Description = "Tous les prix -5% par niveau.", Costs = new[] { 2, 4, 8, 15, 25 } },
        new() { Id = "e_start", Branch = SkillBranch.Economy, Tier = 1, Name = "Capital de départ", Icon = UpgradeIcon.Chest, RequiresId = "e_cost",
                Description = "Commence chaque partie avec 500 / 50K / 5M pièces.", Costs = new[] { 6, 20, 60 } },
        new() { Id = "e_chest", Branch = SkillBranch.Economy, Tier = 2, Name = "Coffres en or", Icon = UpgradeIcon.Chest, RequiresId = "e_start",
                Description = "Coffres en or 30% plus fréquents et 50% plus généreux par niveau.", Costs = new[] { 15, 45 } },
        new() { Id = "e_portal", Branch = SkillBranch.Economy, Tier = 3, Name = "Portail permanent", Icon = UpgradeIcon.Portal, RequiresId = "e_chest",
                Description = "Chaque partie commence avec un portail à placer.", Costs = new[] { 250 } },
    };

    public static SkillNode Find(string id) => Array.Find(Nodes, n => n.Id == id);
}
