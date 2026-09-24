using Godot;
using System;

namespace Plinko;

// Balls are consumables: each one bought drops once, pays out, and is destroyed. Higher
// tiers must be unlocked once, then have a better value-to-price margin.
public class BallTierDef
{
    public int Index;
    public string Name;
    public Color Color;
    public Color Glow;
    public double Value;       // coins per landing, before slot & multipliers
    public double Price;       // price of one ball
    public double UnlockCost;  // one-time cost to unlock the tier
}

public static class BallTiers
{
    public static readonly BallTierDef[] All =
    {
        new() { Index = 0, Name = "Bille", Color = new Color(0.98f, 0.93f, 1f), Glow = Pal.Pink, Value = 1, Price = 1, UnlockCost = 0 },
        new() { Index = 1, Name = "Bille d'argent", Color = new Color(0.82f, 0.88f, 0.96f), Glow = new Color(0.6f, 0.8f, 1f), Value = 8, Price = 7, UnlockCost = 1_000 },
        new() { Index = 2, Name = "Bille d'or", Color = new Color(1f, 0.8f, 0.3f), Glow = Pal.Gold, Value = 64, Price = 50, UnlockCost = 400_000 },
        new() { Index = 3, Name = "Bille de diamant", Color = new Color(0.75f, 1f, 1f), Glow = Pal.Cyan, Value = 512, Price = 350, UnlockCost = 1.5e8 },
        new() { Index = 4, Name = "Bille de rubis", Color = new Color(1f, 0.35f, 0.45f), Glow = Pal.Red, Value = 4_096, Price = 2_500, UnlockCost = 6e10 },
        new() { Index = 5, Name = "Bille cosmique", Color = new Color(0.8f, 0.6f, 1f), Glow = Pal.Purple, Value = 32_768, Price = 17_500, UnlockCost = 2.5e13 },
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
}

public static class Upgrades
{
    public const int BaseRows = 8;

    public static readonly UpgradeDef[] All =
    {
        new()
        {
            Id = IdleUpgrade.AutoDropper, Name = "Distributeur automatique", Icon = UpgradeIcon.Gear,
            Describe = _ => "Lâche tes billes tout seul, en continu (2 billes par seconde au départ).",
            BaseCost = 30, Growth = 1, MaxLevel = 1,
        },
        new()
        {
            Id = IdleUpgrade.Cadence, Name = "Cadence", Icon = UpgradeIcon.Clock,
            Describe = l => $"Le distributeur lâche 25% de billes en plus par seconde (niveau {l + 1}).",
            BaseCost = 40, Growth = 3.2, MaxLevel = 40,
        },
        new()
        {
            Id = IdleUpgrade.Value, Name = "Polissage", Icon = UpgradeIcon.Multiplier,
            Describe = l => $"Toutes les billes rapportent x1,2 (niveau {l + 1}).",
            BaseCost = 100, Growth = 10, MaxLevel = 30,
        },
        new()
        {
            Id = IdleUpgrade.Rows, Name = "+1 Rangée", Icon = UpgradeIcon.Rows,
            Describe = l => $"Les billes s'éparpillent plus et les bords rapportent plus ({BaseRows + l + 1} rangées).",
            BaseCost = 1_000, Growth = 25, MaxLevel = 8,
        },
        new()
        {
            Id = IdleUpgrade.GoldenPegs, Name = "Clous dorés", Icon = UpgradeIcon.Peg,
            Describe = l => $"Chaque clou touché rapporte {(l + 1) * 3}% de la valeur de la bille.",
            BaseCost = 400, Growth = 3.5, MaxLevel = 25,
        },
        new()
        {
            Id = IdleUpgrade.Critical, Name = "Coup critique", Icon = UpgradeIcon.Star,
            Describe = l => $"{(l + 1) * 3}% de chances qu'une bille rapporte x10.",
            BaseCost = 2_500, Growth = 5, MaxLevel = 15,
        },
        new()
        {
            Id = IdleUpgrade.Slots, Name = "Cases renforcées", Icon = UpgradeIcon.NarrowSlots,
            Describe = l => $"Tous les multiplicateurs de cases x1,25 (niveau {l + 1}).",
            BaseCost = 6_000, Growth = 12, MaxLevel = 25,
        },
        new()
        {
            Id = IdleUpgrade.Portal, Name = "Portail dédoubleur", Icon = UpgradeIcon.Portal,
            Describe = l => "Place un portail : chaque bille qui le traverse se dédouble.",
            BaseCost = 2e6, Growth = 150, MaxLevel = 3, AutoBuyable = false,
        },
        new()
        {
            Id = IdleUpgrade.AutoRestock, Name = "Réapprovisionnement auto", Icon = UpgradeIcon.ExtraBalls,
            Describe = _ => "Rachète du stock tout seul, avec le meilleur type de bille abordable.",
            BaseCost = 150, Growth = 1, MaxLevel = 1,
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
                Description = "Commence avec le distributeur et le réapprovisionnement. Cadence +15% par niveau.", Costs = new[] { 2, 6, 12 } },
        new() { Id = "a_balls", Branch = SkillBranch.Automation, Tier = 1, Name = "Grossiste", Icon = UpgradeIcon.ExtraBalls, RequiresId = "a_auto",
                Description = "Les billes coûtent 10% moins cher par niveau.", Costs = new[] { 6, 15, 35 } },
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
