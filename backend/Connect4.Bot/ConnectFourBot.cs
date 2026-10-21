using Connect4.Engine;

namespace Connect4.Bot;

/// <summary>
/// A depth-limited minimax bot with alpha-beta pruning for Connect 4. Operates purely on
/// <see cref="ConnectFourBoard"/>'s public API (cloning for speculative moves), so it has no
/// special access to engine internals and could be swapped for a different bot implementation
/// without touching the engine.
/// </summary>
public static class ConnectFourBot
{
    // Columns ordered center-first so alpha-beta pruning cuts more branches (center moves tend
    // to be strongest in Connect 4, so exploring them first tightens the window fastest).
    private static readonly int[] ColumnSearchOrder = { 3, 2, 4, 1, 5, 0, 6 };

    /// <summary>
    /// Chooses a column for <paramref name="botPlayer"/> to drop into, given the current board.
    /// Assumes it is already <paramref name="botPlayer"/>'s turn.
    /// </summary>
    public static int ChooseColumn(ConnectFourBoard board, Player botPlayer, BotDifficulty difficulty)
    {
        var legalColumns = ColumnSearchOrder.Where(c => board.HeightOf(c) < ConnectFourBoard.Rows).ToList();
        if (legalColumns.Count == 0)
        {
            throw new InvalidOperationException("No legal moves available; the board is full.");
        }

        int depth = difficulty.SearchDepth();
        int bestScore = int.MinValue;
        int bestColumn = legalColumns[0];

        foreach (var column in legalColumns)
        {
            var clone = board.Clone();
            clone.DropPiece(botPlayer, column);
            int score = Minimax(clone, depth - 1, int.MinValue + 1, int.MaxValue - 1, botPlayer, isMaximizing: false);

            if (score > bestScore)
            {
                bestScore = score;
                bestColumn = column;
            }
        }

        return bestColumn;
    }

    private static int Minimax(ConnectFourBoard board, int depth, int alpha, int beta, Player botPlayer, bool isMaximizing)
    {
        if (board.IsOver)
        {
            if (board.Winner == botPlayer) return 1_000_000 + depth; // win sooner scores higher
            if (board.Winner == Opponent(botPlayer)) return -1_000_000 - depth;
            return 0; // draw
        }

        if (depth == 0)
        {
            return Evaluate(board, botPlayer);
        }

        var legalColumns = ColumnSearchOrder.Where(c => board.HeightOf(c) < ConnectFourBoard.Rows).ToList();
        var toMove = isMaximizing ? botPlayer : Opponent(botPlayer);

        if (isMaximizing)
        {
            int best = int.MinValue;
            foreach (var column in legalColumns)
            {
                var clone = board.Clone();
                clone.DropPiece(toMove, column);
                int score = Minimax(clone, depth - 1, alpha, beta, botPlayer, isMaximizing: false);
                best = Math.Max(best, score);
                alpha = Math.Max(alpha, best);
                if (alpha >= beta) break;
            }
            return best;
        }
        else
        {
            int best = int.MaxValue;
            foreach (var column in legalColumns)
            {
                var clone = board.Clone();
                clone.DropPiece(toMove, column);
                int score = Minimax(clone, depth - 1, alpha, beta, botPlayer, isMaximizing: true);
                best = Math.Min(best, score);
                beta = Math.Min(beta, best);
                if (alpha >= beta) break;
            }
            return best;
        }
    }

    private static Player Opponent(Player player) => player == Player.Red ? Player.Yellow : Player.Red;

    /// <summary>
    /// Heuristic score for non-terminal positions: scans every 4-cell window (horizontal,
    /// vertical, both diagonals) and scores it by how many of the bot's/opponent's pieces it
    /// contains, plus a small bonus for center-column control.
    /// </summary>
    private static int Evaluate(ConnectFourBoard board, Player botPlayer)
    {
        var opponent = Opponent(botPlayer);
        int score = 0;

        const int centerColumn = ConnectFourBoard.Columns / 2;
        for (int row = 0; row < ConnectFourBoard.Rows; row++)
        {
            if (board.GetCell(row, centerColumn) == botPlayer) score += 3;
            else if (board.GetCell(row, centerColumn) == opponent) score -= 3;
        }

        var directions = new (int dRow, int dCol)[] { (0, 1), (1, 0), (1, 1), (1, -1) };
        foreach (var (dRow, dCol) in directions)
        {
            for (int row = 0; row < ConnectFourBoard.Rows; row++)
            {
                for (int col = 0; col < ConnectFourBoard.Columns; col++)
                {
                    int endRow = row + dRow * 3;
                    int endCol = col + dCol * 3;
                    if (endRow < 0 || endRow >= ConnectFourBoard.Rows || endCol < 0 || endCol >= ConnectFourBoard.Columns)
                    {
                        continue;
                    }

                    score += ScoreWindow(board, row, col, dRow, dCol, botPlayer, opponent);
                }
            }
        }

        return score;
    }

    private static int ScoreWindow(ConnectFourBoard board, int row, int col, int dRow, int dCol, Player botPlayer, Player opponent)
    {
        int botCount = 0, opponentCount = 0, emptyCount = 0;

        for (int i = 0; i < 4; i++)
        {
            var cell = board.GetCell(row + dRow * i, col + dCol * i);
            if (cell == botPlayer) botCount++;
            else if (cell == opponent) opponentCount++;
            else emptyCount++;
        }

        if (botCount > 0 && opponentCount > 0)
        {
            return 0; // contested window, nobody can complete 4 here
        }

        if (botCount == 4) return 100_000; // shouldn't normally reach here (terminal check), but safe
        if (botCount == 3 && emptyCount == 1) return 50;
        if (botCount == 2 && emptyCount == 2) return 10;

        if (opponentCount == 4) return -100_000;
        if (opponentCount == 3 && emptyCount == 1) return -80; // weight slightly higher: block real threats
        if (opponentCount == 2 && emptyCount == 2) return -10;

        return 0;
    }
}
