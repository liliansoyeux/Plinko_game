namespace Plinko;

// A cocktail: a passive bonus that lives on the machine's shelf for the rest of the run.
// Every hook has a no-op default so each cocktail only overrides the moments it cares about.
public interface IRunModifier
{
    string Id { get; }
    string DisplayName { get; }
    string Description { get; }
    Godot.Color LiquidColor { get; }

    void OnAcquired(RunStats stats) { }
    void OnBallSpawn(Ball ball, RunStats stats) { }
    float ModifyPayout(Ball ball, float slotMultiplier, float payout) => payout;
    void OnSlotHit(Ball ball, float slotMultiplier) { }
    void OnPegHit(Ball ball) { }
    void OnLevelUp(int newLevel) { }
    void OnPalierStart(PalierDef palier) { }
}
