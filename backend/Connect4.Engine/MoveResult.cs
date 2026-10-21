namespace Connect4.Engine;

public enum MoveStatus
{
    Ok,
    WrongTurn,
    ColumnFull,
    InvalidColumn,
    GameAlreadyOver
}

public readonly record struct Cell(int Row, int Column);

public sealed class MoveResult
{
    public required MoveStatus Status { get; init; }
    public int Row { get; init; } = -1;
    public int Column { get; init; } = -1;
    public Player MovedBy { get; init; } = Player.None;
    public bool IsWin { get; init; }
    public bool IsDraw { get; init; }
    public Player Winner { get; init; } = Player.None;
    public IReadOnlyList<Cell> WinningCells { get; init; } = Array.Empty<Cell>();

    public static MoveResult Failure(MoveStatus status) => new() { Status = status };
}
