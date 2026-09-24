namespace Plinko;

// Boss-only board hardships. Applied to a transient copy of RunStats at board-generation
// time only (see PlinkoBoard.BuildEffectiveStats) — never mutates the player's permanent
// upgrades, so they carry over unaffected once the boss palier ends.
public enum BossModifierKind
{
    ExtraBlockers,
    NarrowerSlots,
    SpinningBlockers,
    FewerBalls,
    ShrunkenJackpots,
}

public class BossModifier
{
    public BossModifierKind Kind;
    public float Value;

    public string Label => Kind switch
    {
        BossModifierKind.ExtraBlockers => $"+{(int)Value} bâtons sur le plateau",
        BossModifierKind.NarrowerSlots => $"Cases {(int)((1f - Value) * 100f + 0.5f)}% plus étroites",
        BossModifierKind.SpinningBlockers => $"{(int)Value} bâtons tournants",
        BossModifierKind.FewerBalls => $"{(int)Value} billes en moins",
        BossModifierKind.ShrunkenJackpots => "Jackpots divisés par 2",
        _ => Kind.ToString()
    };
}
