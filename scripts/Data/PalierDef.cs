using System;
using System.Collections.Generic;

namespace Plinko;

public class PalierDef
{
    public int Index;
    public float ScoreTarget;
    public int BallCount;
    public bool IsBoss;
    public List<BossModifier> Modifiers = new();

    public bool Has(BossModifierKind kind) => Modifiers.Exists(m => m.Kind == kind);
}

public static class PalierGenerator
{
    public const int BossEveryNPaliers = 5;

    public static float TargetBase = 32f;
    public static float TargetGrowth = 1.4f;

    public static PalierDef Generate(int index)
    {
        bool isBoss = (index + 1) % BossEveryNPaliers == 0;

        float target = TargetBase * MathF.Pow(TargetGrowth, index);
        if (isBoss)
        {
            target *= 1.25f;
        }

        var palier = new PalierDef
        {
            Index = index,
            ScoreTarget = MathF.Round(target / 5f) * 5f,
            BallCount = Math.Max(6, 12 - index / 3),
            IsBoss = isBoss
        };

        if (isBoss)
        {
            // Two distinct hardships per boss, harder ones unlocking as the run goes on.
            var pool = new List<BossModifier>
            {
                new() { Kind = BossModifierKind.ExtraBlockers, Value = 3f + index / 10 },
                new() { Kind = BossModifierKind.NarrowerSlots, Value = 0.8f },
                new() { Kind = BossModifierKind.SpinningBlockers, Value = 2f + index / 10 },
            };
            if (index >= 9)
            {
                pool.Add(new BossModifier { Kind = BossModifierKind.FewerBalls, Value = 2f });
                pool.Add(new BossModifier { Kind = BossModifierKind.ShrunkenJackpots, Value = 0.5f });
            }

            var rng = new Random(index * 7919 + 17);
            for (int i = 0; i < 2 && pool.Count > 0; i++)
            {
                int pick = rng.Next(pool.Count);
                palier.Modifiers.Add(pool[pick]);
                pool.RemoveAt(pick);
            }

            foreach (var modifier in palier.Modifiers)
            {
                if (modifier.Kind == BossModifierKind.FewerBalls)
                {
                    palier.BallCount -= (int)modifier.Value;
                }
            }
        }

        return palier;
    }
}
