namespace Connect4.Bot;

public enum BotDifficulty
{
    Easy,
    Medium,
    Hard
}

public static class BotDifficultyExtensions
{
    /// <summary>
    /// Search depth (plies) used for each difficulty. Higher = stronger but slower.
    /// </summary>
    public static int SearchDepth(this BotDifficulty difficulty) => difficulty switch
    {
        BotDifficulty.Easy => 2,
        BotDifficulty.Medium => 4,
        BotDifficulty.Hard => 7,
        _ => 4
    };
}
