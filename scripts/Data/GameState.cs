namespace Plinko;

public enum GameState
{
    Idle,
    Playing,
    PalierCleared,
    GameOver
}

public enum BoardAction
{
    ReplaceWorstSlot,
    BoostBestSlot,
    CurseGoodSlot,
    PlaceBlocker,
    PlacePortal,
}
