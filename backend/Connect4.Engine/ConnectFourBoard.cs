namespace Connect4.Engine;

/// <summary>
/// Pure, server-authoritative Connect 4 board logic. No transport or storage concerns.
/// </summary>
public sealed class ConnectFourBoard
{
    public const int Columns = 7;
    public const int Rows = 6;
    private const int ConnectLength = 4;

    private readonly Player[,] _cells = new Player[Rows, Columns];
    private int[] _columnHeights = new int[Columns];

    public Player CurrentTurn { get; private set; }
    public bool IsOver { get; private set; }

    public ConnectFourBoard(Player startingPlayer = Player.Red)
    {
        CurrentTurn = startingPlayer;
    }
    public Player Winner { get; private set; } = Player.None;
    public IReadOnlyList<Cell> WinningCells { get; private set; } = Array.Empty<Cell>();
    public int MoveCount { get; private set; }

    public Player GetCell(int row, int column) => _cells[row, column];

    /// <summary>
    /// Attempts to drop a piece for <paramref name="player"/> into <paramref name="column"/>.
    /// All validation (turn order, bounds, column full, game over) happens here.
    /// </summary>
    public MoveResult DropPiece(Player player, int column)
    {
        if (IsOver)
        {
            return MoveResult.Failure(MoveStatus.GameAlreadyOver);
        }

        if (column < 0 || column >= Columns)
        {
            return MoveResult.Failure(MoveStatus.InvalidColumn);
        }

        if (player != CurrentTurn)
        {
            return MoveResult.Failure(MoveStatus.WrongTurn);
        }

        int row = Rows - 1 - _columnHeights[column];
        if (row < 0)
        {
            return MoveResult.Failure(MoveStatus.ColumnFull);
        }

        _cells[row, column] = player;
        _columnHeights[column]++;
        MoveCount++;

        var winningCells = FindWinningCells(row, column, player);
        if (winningCells.Count > 0)
        {
            IsOver = true;
            Winner = player;
            WinningCells = winningCells;
            return new MoveResult
            {
                Status = MoveStatus.Ok,
                Row = row,
                Column = column,
                MovedBy = player,
                IsWin = true,
                Winner = player,
                WinningCells = winningCells
            };
        }

        if (MoveCount == Rows * Columns)
        {
            IsOver = true;
            return new MoveResult
            {
                Status = MoveStatus.Ok,
                Row = row,
                Column = column,
                MovedBy = player,
                IsDraw = true
            };
        }

        CurrentTurn = player == Player.Red ? Player.Yellow : Player.Red;

        return new MoveResult
        {
            Status = MoveStatus.Ok,
            Row = row,
            Column = column,
            MovedBy = player
        };
    }

    private List<Cell> FindWinningCells(int row, int column, Player player)
    {
        // directions: horizontal, vertical, diagonal "/", diagonal "\"
        var directions = new (int dRow, int dCol)[] { (0, 1), (1, 0), (1, 1), (1, -1) };

        foreach (var (dRow, dCol) in directions)
        {
            var line = new List<Cell> { new(row, column) };

            // walk forward
            int r = row + dRow, c = column + dCol;
            while (InBounds(r, c) && _cells[r, c] == player)
            {
                line.Add(new Cell(r, c));
                r += dRow;
                c += dCol;
            }

            // walk backward
            r = row - dRow;
            c = column - dCol;
            while (InBounds(r, c) && _cells[r, c] == player)
            {
                line.Add(new Cell(r, c));
                r -= dRow;
                c -= dCol;
            }

            if (line.Count >= ConnectLength)
            {
                return line.Take(ConnectLength).ToList();
            }
        }

        return new List<Cell>();
    }

    private static bool InBounds(int row, int col) => row >= 0 && row < Rows && col >= 0 && col < Columns;

    public Player[,] Snapshot()
    {
        var copy = new Player[Rows, Columns];
        Array.Copy(_cells, copy, _cells.Length);
        return copy;
    }

    public int HeightOf(int column) => _columnHeights[column];

    /// <summary>
    /// Deep copy for speculative search (e.g. a bot exploring hypothetical moves) without
    /// mutating this instance.
    /// </summary>
    public ConnectFourBoard Clone()
    {
        var clone = new ConnectFourBoard(CurrentTurn)
        {
            IsOver = IsOver,
            Winner = Winner,
            WinningCells = WinningCells,
            MoveCount = MoveCount
        };
        Array.Copy(_cells, clone._cells, _cells.Length);
        Array.Copy(_columnHeights, clone._columnHeights, _columnHeights.Length);
        return clone;
    }
}
