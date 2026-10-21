using Connect4.Engine;
using Xunit;

namespace Connect4.Engine.Tests;

public class ConnectFourBoardTests
{
    [Fact]
    public void NewBoard_RedGoesFirst()
    {
        var board = new ConnectFourBoard();
        Assert.Equal(Player.Red, board.CurrentTurn);
        Assert.False(board.IsOver);
    }

    [Fact]
    public void DropPiece_OutOfTurn_ReturnsWrongTurn()
    {
        var board = new ConnectFourBoard();
        var result = board.DropPiece(Player.Yellow, 0);
        Assert.Equal(MoveStatus.WrongTurn, result.Status);
    }

    [Fact]
    public void DropPiece_InvalidColumn_ReturnsInvalidColumn()
    {
        var board = new ConnectFourBoard();
        Assert.Equal(MoveStatus.InvalidColumn, board.DropPiece(Player.Red, -1).Status);
        Assert.Equal(MoveStatus.InvalidColumn, board.DropPiece(Player.Red, 7).Status);
    }

    [Fact]
    public void DropPiece_ValidMove_StacksOnLowestEmptyCell()
    {
        var board = new ConnectFourBoard();
        var first = board.DropPiece(Player.Red, 3);
        Assert.Equal(MoveStatus.Ok, first.Status);
        Assert.Equal(5, first.Row); // bottom row
        Assert.Equal(Player.Yellow, board.CurrentTurn);

        var second = board.DropPiece(Player.Yellow, 3);
        Assert.Equal(4, second.Row); // stacked on top
        Assert.Equal(Player.Red, board.CurrentTurn);
    }

    [Fact]
    public void DropPiece_ColumnFull_ReturnsColumnFull()
    {
        var board = new ConnectFourBoard();
        for (int i = 0; i < ConnectFourBoard.Rows; i++)
        {
            var player = i % 2 == 0 ? Player.Red : Player.Yellow;
            var result = board.DropPiece(player, 0);
            Assert.Equal(MoveStatus.Ok, result.Status);
        }

        var nextPlayer = board.CurrentTurn;
        var overflow = board.DropPiece(nextPlayer, 0);
        Assert.Equal(MoveStatus.ColumnFull, overflow.Status);
    }

    [Fact]
    public void DropPiece_AfterGameOver_ReturnsGameAlreadyOver()
    {
        var board = new ConnectFourBoard();
        var result = PlayHorizontalWin(board);
        Assert.True(board.IsOver);
        Assert.True(result.IsWin);

        var next = board.DropPiece(Player.Yellow, 4);
        Assert.Equal(MoveStatus.GameAlreadyOver, next.Status);
    }

    [Fact]
    public void HorizontalWin_IsDetected()
    {
        var board = new ConnectFourBoard();
        var result = PlayHorizontalWin(board);

        Assert.True(result.IsWin);
        Assert.Equal(Player.Red, result.Winner);
        Assert.Equal(4, result.WinningCells.Count);
        Assert.All(result.WinningCells, c => Assert.Equal(5, c.Row));
        var cols = result.WinningCells.Select(c => c.Column).OrderBy(c => c).ToArray();
        Assert.Equal(new[] { 0, 1, 2, 3 }, cols);
    }

    private static MoveResult PlayHorizontalWin(ConnectFourBoard board)
    {
        // Red drops at columns 0,1,2,3 on the bottom row; Yellow drops in column 6 between turns.
        MoveResult last = null!;
        for (int col = 0; col < 4; col++)
        {
            last = board.DropPiece(Player.Red, col);
            if (col < 3)
            {
                board.DropPiece(Player.Yellow, 6);
            }
        }
        return last;
    }

    [Fact]
    public void VerticalWin_IsDetected()
    {
        var board = new ConnectFourBoard();
        MoveResult last = null!;
        for (int i = 0; i < 4; i++)
        {
            last = board.DropPiece(Player.Red, 2);
            if (i < 3)
            {
                board.DropPiece(Player.Yellow, 3);
            }
        }

        Assert.True(last.IsWin);
        Assert.Equal(Player.Red, last.Winner);
        Assert.All(last.WinningCells, c => Assert.Equal(2, c.Column));
        var rows = last.WinningCells.Select(c => c.Row).OrderBy(r => r).ToArray();
        Assert.Equal(new[] { 2, 3, 4, 5 }, rows);
    }

    [Fact]
    public void DiagonalUpRightWin_IsDetected()
    {
        var board = new ConnectFourBoard();
        // Builds a "/" diagonal for Red at (5,0) (4,1) (3,2) (2,3).
        board.DropPiece(Player.Red, 0);    // (5,0) R
        board.DropPiece(Player.Yellow, 1); // (5,1) Y
        board.DropPiece(Player.Red, 1);    // (4,1) R
        board.DropPiece(Player.Yellow, 2); // (5,2) Y
        board.DropPiece(Player.Red, 2);    // (4,2) R
        board.DropPiece(Player.Yellow, 3); // (5,3) Y
        board.DropPiece(Player.Red, 2);    // (3,2) R
        board.DropPiece(Player.Yellow, 3); // (4,3) Y
        board.DropPiece(Player.Red, 6);    // (5,6) R filler, keeps alternation intact
        board.DropPiece(Player.Yellow, 3); // (3,3) Y
        var result = board.DropPiece(Player.Red, 3); // (2,3) R -> completes the diagonal

        Assert.True(result.IsWin);
        Assert.Equal(Player.Red, result.Winner);
        var expected = new[] { (5, 0), (4, 1), (3, 2), (2, 3) };
        var actual = result.WinningCells.Select(c => (c.Row, c.Column)).OrderBy(t => t.Item2).ToArray();
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void DiagonalDownRightWin_IsDetected()
    {
        var board = new ConnectFourBoard();
        // Builds a "\" diagonal for Red at (2,0) (3,1) (4,2) (5,3).
        board.DropPiece(Player.Red, 3);    // (5,3) R
        board.DropPiece(Player.Yellow, 2); // (5,2) Y
        board.DropPiece(Player.Red, 2);    // (4,2) R
        board.DropPiece(Player.Yellow, 1); // (5,1) Y
        board.DropPiece(Player.Red, 1);    // (4,1) R
        board.DropPiece(Player.Yellow, 0); // (5,0) Y
        board.DropPiece(Player.Red, 1);    // (3,1) R
        board.DropPiece(Player.Yellow, 0); // (4,0) Y
        board.DropPiece(Player.Red, 0);    // (3,0) R
        board.DropPiece(Player.Yellow, 6); // (5,6) Y filler
        var result = board.DropPiece(Player.Red, 0); // (2,0) R -> completes the diagonal

        Assert.True(result.IsWin);
        Assert.Equal(Player.Red, result.Winner);
        var expected = new[] { (2, 0), (3, 1), (4, 2), (5, 3) };
        var actual = result.WinningCells.Select(c => (c.Row, c.Column)).OrderBy(t => t.Item1).ToArray();
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void FullBoard_WithNoFourInARow_EndsInDraw()
    {
        var columnSequence = DrawBoardSearch.FindDrawColumnSequence();
        var board = new ConnectFourBoard();

        MoveResult last = null!;
        foreach (var column in columnSequence)
        {
            last = board.DropPiece(board.CurrentTurn, column);
            Assert.Equal(MoveStatus.Ok, last.Status);
        }

        Assert.True(board.IsOver);
        Assert.False(last.IsWin);
        Assert.True(last.IsDraw);
        Assert.Equal(Player.None, board.Winner);
        Assert.Equal(ConnectFourBoard.Rows * ConnectFourBoard.Columns, board.MoveCount);
    }

    /// <summary>
    /// Finds, via backtracking over a plain grid, a sequence of column drops that fills the
    /// entire 7x6 board without ever producing four-in-a-row for either player.
    /// </summary>
    private static class DrawBoardSearch
    {
        public static int[] FindDrawColumnSequence()
        {
            var heights = new int[ConnectFourBoard.Columns];
            var grid = new Player[ConnectFourBoard.Rows, ConnectFourBoard.Columns];
            var moves = new List<int>();

            if (!Search(grid, heights, Player.Red, moves))
            {
                throw new InvalidOperationException("No draw-safe fill order found.");
            }

            return moves.ToArray();
        }

        private static bool Search(Player[,] grid, int[] heights, Player toMove, List<int> moves)
        {
            if (moves.Count == ConnectFourBoard.Rows * ConnectFourBoard.Columns)
            {
                return true;
            }

            for (int col = 0; col < ConnectFourBoard.Columns; col++)
            {
                if (heights[col] >= ConnectFourBoard.Rows)
                {
                    continue;
                }

                int row = ConnectFourBoard.Rows - 1 - heights[col];
                grid[row, col] = toMove;
                heights[col]++;
                moves.Add(col);

                if (!CreatesFourInARow(grid, row, col, toMove))
                {
                    var next = toMove == Player.Red ? Player.Yellow : Player.Red;
                    if (Search(grid, heights, next, moves))
                    {
                        return true;
                    }
                }

                moves.RemoveAt(moves.Count - 1);
                heights[col]--;
                grid[row, col] = Player.None;
            }

            return false;
        }

        private static bool CreatesFourInARow(Player[,] grid, int row, int col, Player player)
        {
            var directions = new (int dRow, int dCol)[] { (0, 1), (1, 0), (1, 1), (1, -1) };
            foreach (var (dRow, dCol) in directions)
            {
                int count = 1;
                count += CountDirection(grid, row, col, dRow, dCol, player);
                count += CountDirection(grid, row, col, -dRow, -dCol, player);
                if (count >= 4)
                {
                    return true;
                }
            }
            return false;
        }

        private static int CountDirection(Player[,] grid, int row, int col, int dRow, int dCol, Player player)
        {
            int count = 0;
            int r = row + dRow, c = col + dCol;
            while (r >= 0 && r < ConnectFourBoard.Rows && c >= 0 && c < ConnectFourBoard.Columns && grid[r, c] == player)
            {
                count++;
                r += dRow;
                c += dCol;
            }
            return count;
        }
    }
}
