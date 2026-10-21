using Connect4.Bot;
using Connect4.Engine;
using Xunit;

namespace Connect4.Engine.Tests;

public class ConnectFourBotTests
{
    [Fact]
    public void Bot_TakesImmediateWinningMove_WhenAvailable()
    {
        var board = new ConnectFourBoard();
        // Red has three in a row at columns 0,1,2 (bottom row); Yellow pieces are elsewhere.
        board.DropPiece(Player.Red, 0);
        board.DropPiece(Player.Yellow, 6);
        board.DropPiece(Player.Red, 1);
        board.DropPiece(Player.Yellow, 6);
        board.DropPiece(Player.Red, 2);
        board.DropPiece(Player.Yellow, 5);
        // It's Red's turn; dropping in column 3 completes four in a row.

        var chosen = ConnectFourBot.ChooseColumn(board, Player.Red, BotDifficulty.Medium);

        Assert.Equal(3, chosen);
    }

    [Fact]
    public void Bot_BlocksOpponentsImmediateWin_WhenNoWinOfItsOwnIsAvailable()
    {
        var board = new ConnectFourBoard();
        // Yellow has three in a row at columns 0,1,2 (bottom row) and is one move from winning
        // at column 3. Red (the bot) must block there.
        board.DropPiece(Player.Red, 6);
        board.DropPiece(Player.Yellow, 0);
        board.DropPiece(Player.Red, 5);
        board.DropPiece(Player.Yellow, 1);
        board.DropPiece(Player.Red, 4);
        board.DropPiece(Player.Yellow, 2);
        // It's Red's turn; Yellow threatens to win at column 3 next.

        var chosen = ConnectFourBot.ChooseColumn(board, Player.Red, BotDifficulty.Medium);

        Assert.Equal(3, chosen);
    }

    [Fact]
    public void Bot_NeverReturnsAFullColumn()
    {
        var board = new ConnectFourBoard();
        for (int i = 0; i < ConnectFourBoard.Rows; i++)
        {
            var player = board.CurrentTurn;
            board.DropPiece(player, 0);
        }
        // Column 0 is now full; the bot (whichever color's turn it is) must not choose it.

        var chosen = ConnectFourBot.ChooseColumn(board, board.CurrentTurn, BotDifficulty.Easy);

        Assert.NotEqual(0, chosen);
    }
}
