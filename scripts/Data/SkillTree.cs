using System;
using System.Collections.Generic;

namespace Plinko;

public enum SkillBranch
{
    Fortune,
    Chance,
    Safety,
}

public class SkillNode
{
    public string Id;
    public SkillBranch Branch;
    public int Tier;              // 0 = top of the branch, 3 = capstone
    public string Name;
    public string Description;    // per level
    public UpgradeIcon Icon;
    public int[] Costs;           // one entry per level
    public string RequiresId;     // previous node in the branch (needs at least one level)
    public Action<RunStats, int> Apply = (_, _) => { };

    public int MaxLevel => Costs.Length;
}

// Permanent meta-progression: chips earned at the end of every run are spent here, and the
// bought levels are applied to every new run's starting stats.
public static class SkillTree
{
    public static readonly SkillNode[] Nodes =
    {
        // ---- Fortune : score
        new()
        {
            Id = "fortune_gains", Branch = SkillBranch.Fortune, Tier = 0, Name = "Mise de départ",
            Description = "Gains +5% par niveau.", Icon = UpgradeIcon.Multiplier, Costs = new[] { 8, 16, 28 },
            Apply = (s, l) => s.GlobalMultiplierModifier *= 1f + 0.05f * l,
        },
        new()
        {
            Id = "fortune_gold", Branch = SkillBranch.Fortune, Tier = 1, Name = "Pièces d'or",
            Description = "+4% de billes dorées par niveau.", Icon = UpgradeIcon.GoldBall, Costs = new[] { 12, 22, 35 },
            RequiresId = "fortune_gains",
            Apply = (s, l) => s.GoldenBallChance += 0.04f * l,
        },
        new()
        {
            Id = "fortune_jackpot", Branch = SkillBranch.Fortune, Tier = 2, Name = "Jackpot étincelant",
            Description = "Jackpots (x10+) +15% par niveau.", Icon = UpgradeIcon.Star, Costs = new[] { 30, 50 },
            RequiresId = "fortune_gold",
            Apply = (s, l) => s.JackpotBonus += 0.15f * l,
        },
        new()
        {
            Id = "fortune_portal", Branch = SkillBranch.Fortune, Tier = 3, Name = "Portail d'ouverture",
            Description = "Chaque partie commence avec un portail dédoubleur à placer.", Icon = UpgradeIcon.Portal, Costs = new[] { 120 },
            RequiresId = "fortune_jackpot",
        },

        // ---- Chance : XP & chests
        new()
        {
            Id = "luck_xp", Branch = SkillBranch.Chance, Tier = 0, Name = "Apprenti joueur",
            Description = "Expérience +10% par niveau.", Icon = UpgradeIcon.Xp, Costs = new[] { 8, 16, 28 },
            Apply = (s, l) => s.XpMultiplier *= 1f + 0.1f * l,
        },
        new()
        {
            Id = "luck_chest", Branch = SkillBranch.Chance, Tier = 1, Name = "Coffres garnis",
            Description = "Coffres rares, épiques et légendaires plus fréquents.", Icon = UpgradeIcon.Chest, Costs = new[] { 12, 22, 35 },
            RequiresId = "luck_xp",
            Apply = (s, l) => s.ChestLuck += 0.05f * l,
        },
        new()
        {
            Id = "luck_reroll", Branch = SkillBranch.Chance, Tier = 2, Name = "Relance",
            Description = "Relancer les cartes d'un coffre, 1 fois par partie et par niveau.", Icon = UpgradeIcon.Reroll, Costs = new[] { 30, 55 },
            RequiresId = "luck_chest",
            Apply = (s, l) => s.Rerolls += l,
        },
        new()
        {
            Id = "luck_cards", Branch = SkillBranch.Chance, Tier = 3, Name = "Quatrième carte",
            Description = "Chaque coffre propose 4 cartes au lieu de 3.", Icon = UpgradeIcon.Cards, Costs = new[] { 110 },
            RequiresId = "luck_reroll",
            Apply = (s, l) => s.ChestChoices += l,
        },

        // ---- Sécurité : balls & defence
        new()
        {
            Id = "safe_balls", Branch = SkillBranch.Safety, Tier = 0, Name = "Poches profondes",
            Description = "+1 bille par palier et par niveau.", Icon = UpgradeIcon.ExtraBalls, Costs = new[] { 10, 20, 32 },
            Apply = (s, l) => s.BonusBallsPerPalier += l,
        },
        new()
        {
            Id = "safe_shield", Branch = SkillBranch.Safety, Tier = 1, Name = "Bouclier",
            Description = "Mauvaises cases +10% par niveau.", Icon = UpgradeIcon.Shield, Costs = new[] { 12, 22, 35 },
            RequiresId = "safe_balls",
            Apply = (s, l) => s.NegativePenaltyReduction += 0.1f * l,
        },
        new()
        {
            Id = "safe_malus", Branch = SkillBranch.Safety, Tier = 2, Name = "Assurance",
            Description = "Jauge de malus 15% plus lente par niveau.", Icon = UpgradeIcon.ShieldBroken, Costs = new[] { 28, 48 },
            RequiresId = "safe_shield",
            Apply = (s, l) => s.MalusXpMultiplier *= 1f - 0.15f * l,
        },
        new()
        {
            Id = "safe_second", Branch = SkillBranch.Safety, Tier = 3, Name = "Seconde chance",
            Description = "Une fois par partie : palier raté à 60%+ de l'objectif ? +3 billes au lieu du game over.", Icon = UpgradeIcon.Heart, Costs = new[] { 120 },
            RequiresId = "safe_malus",
        },
    };

    public static SkillNode Find(string id) => Array.Find(Nodes, n => n.Id == id);

    public static int Level(string id) => SaveData.SkillLevel(id);
    public static bool Has(string id) => Level(id) > 0;

    public static int NextCost(SkillNode node)
    {
        int level = Level(node.Id);
        return level >= node.MaxLevel ? -1 : node.Costs[level];
    }

    public static bool IsUnlocked(SkillNode node) => node.RequiresId == null || Has(node.RequiresId);

    public static bool CanBuy(SkillNode node)
    {
        int cost = NextCost(node);
        return cost >= 0 && IsUnlocked(node) && SaveData.Chips >= cost;
    }

    public static bool Buy(SkillNode node)
    {
        if (!CanBuy(node))
        {
            return false;
        }
        SaveData.AddChips(-NextCost(node));
        SaveData.SetSkillLevel(node.Id, Level(node.Id) + 1);
        return true;
    }

    public static int SpentChips()
    {
        int spent = 0;
        foreach (var node in Nodes)
        {
            for (int i = 0; i < Level(node.Id); i++)
            {
                spent += node.Costs[i];
            }
        }
        return spent;
    }

    public static void ResetAll()
    {
        SaveData.AddChips(SpentChips());
        foreach (var node in Nodes)
        {
            SaveData.SetSkillLevel(node.Id, 0);
        }
    }

    public static void ApplyToStats(RunStats stats)
    {
        foreach (var node in Nodes)
        {
            int level = Level(node.Id);
            if (level > 0)
            {
                node.Apply(stats, level);
            }
        }
    }

    // Chips earned by a finished (or abandoned) run.
    public static (int total, int fromPaliers, int fromBosses, int fromLevel) RunReward(int paliersCleared, int bossesBeaten, int level)
    {
        int paliers = paliersCleared * 3;
        int bosses = bossesBeaten * 6;
        int levels = Math.Max(0, level - 1) / 2;
        return (paliers + bosses + levels, paliers, bosses, levels);
    }
}
