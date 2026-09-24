using System;
using System.Collections.Generic;

namespace Plinko;

// Builds each run's slot values. Values are drawn at random from weighted tiers, but always
// with as many good slots (>x1) as bad ones (<x1), plus one x1 when the count is odd. The
// shuffled layout is then scored with a Plinko's binomial landing distribution and rerolled
// until it's fair: not unplayable (good slots all at the edges) nor trivial (x110 dead centre).
public static class SlotLayout
{
    private const float MinCentreValue = 4.3f;
    // Income without jackpots (values capped at x10), averaged over every aim position:
    // guarantees steady mid-value slots within reach, so a run doesn't hinge on one x41.
    private const float MinSteadyValue = 3.6f;
    private const float JackpotCap = 10f;
    private const float MinBestAimValue = 6.5f;
    private const float MaxBestAimValue = 14f;
    private const int MaxAttempts = 600;

    private static readonly (float value, int weight)[] GoodValues =
    {
        (2f, 2), (3f, 3), (4f, 3), (5f, 3), (8f, 3), (10f, 3), (15f, 2), (20f, 1), (25f, 1),
    };

    private static readonly (float value, int weight)[] JackpotValues =
    {
        (41f, 3), (60f, 2), (110f, 3),
    };

    private static readonly (float value, int weight)[] BadValues =
    {
        (0.1f, 2), (0.2f, 3), (0.3f, 3), (0.4f, 2), (0.5f, 3), (0.6f, 2), (0.7f, 2), (0.8f, 1),
    };

    public static List<float> Generate(int slotCount, int rows, Random rng)
    {
        List<float> best = null;
        float bestDistance = float.MaxValue;

        for (int attempt = 0; attempt < MaxAttempts; attempt++)
        {
            var layout = Draw(slotCount, rng);
            float centre = ExpectedValue(layout, rows, 0f);
            float bestAim = centre;
            foreach (float aim in new[] { -1.5f, -0.75f, 0.75f, 1.5f })
            {
                bestAim = Math.Max(bestAim, ExpectedValue(layout, rows, aim));
            }

            float steady = 0f;
            foreach (float aim in new[] { -1.5f, -0.75f, 0f, 0.75f, 1.5f })
            {
                steady += ExpectedValue(layout, rows, aim, JackpotCap) / 5f;
            }

            if (centre >= MinCentreValue && steady >= MinSteadyValue && bestAim >= MinBestAimValue && bestAim <= MaxBestAimValue)
            {
                return layout;
            }

            float distance = Math.Max(0f, MinCentreValue - centre)
                + Math.Max(0f, MinSteadyValue - steady) * 2f
                + Math.Max(0f, MinBestAimValue - bestAim)
                + Math.Max(0f, bestAim - MaxBestAimValue);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = layout;
            }
        }
        return best;
    }

    private static List<float> Draw(int slotCount, Random rng)
    {
        int neutral = slotCount % 2;
        int perSide = (slotCount - neutral) / 2;
        int jackpots = Math.Min(perSide, rng.NextDouble() < 0.5 ? 2 : 1);

        var layout = new List<float>(slotCount);
        for (int i = 0; i < jackpots; i++)
        {
            layout.Add(Pick(JackpotValues, rng));
        }
        for (int i = jackpots; i < perSide; i++)
        {
            layout.Add(Pick(GoodValues, rng));
        }
        for (int i = 0; i < perSide; i++)
        {
            layout.Add(Pick(BadValues, rng));
        }
        if (neutral == 1)
        {
            layout.Add(1f);
        }

        for (int i = layout.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (layout[i], layout[j]) = (layout[j], layout[i]);
        }
        return layout;
    }

    // Value for a slot added when the board grows: restores the good/bad balance.
    public static float ExtensionValue(IReadOnlyList<float> layout, Random rng)
    {
        int good = 0;
        int bad = 0;
        foreach (float v in layout)
        {
            if (v > 1f) good++;
            else if (v < 1f) bad++;
        }
        bool addGood = good < bad || (good == bad && rng.NextDouble() < 0.5);
        if (!addGood)
        {
            return Pick(BadValues, rng);
        }
        return rng.NextDouble() < 0.12 ? Pick(JackpotValues, rng) : Pick(GoodValues, rng);
    }

    private static float Pick((float value, int weight)[] table, Random rng)
    {
        int total = 0;
        foreach (var (_, weight) in table)
        {
            total += weight;
        }
        int roll = rng.Next(total);
        foreach (var (value, weight) in table)
        {
            if (roll < weight)
            {
                return value;
            }
            roll -= weight;
        }
        return table[^1].value;
    }

    // Expected payout per ball when aiming `aimOffset` slots off-centre: each row nudges
    // the ball half a slot left or right, so the landing offset is binomial.
    public static float ExpectedValue(IReadOnlyList<float> layout, int rows, float aimOffset, float cap = float.MaxValue)
    {
        int n = layout.Count;
        float centre = (n - 1) / 2f + aimOffset;
        double ev = 0;
        double total = Math.Pow(2, rows);
        double combination = 1;
        for (int k = 0; k <= rows; k++)
        {
            double p = combination / total;
            float pos = centre + (k - rows / 2f);
            int i0 = (int)Math.Floor(pos);
            float frac = pos - i0;
            ev += p * ((1f - frac) * Math.Min(cap, ValueAt(layout, i0)) + frac * Math.Min(cap, ValueAt(layout, i0 + 1)));
            combination = combination * (rows - k) / (k + 1);
        }
        return (float)ev;
    }

    private static float ValueAt(IReadOnlyList<float> layout, int index)
    {
        // Walls bounce balls back in, so off-board mass lands on the edge slot.
        index = Math.Clamp(index, 0, layout.Count - 1);
        return layout[index];
    }
}
