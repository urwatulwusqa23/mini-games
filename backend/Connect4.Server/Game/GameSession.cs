using Connect4.Engine;
using Connect4.Server.Models;
using BotDifficulty = Connect4.Bot.BotDifficulty;

namespace Connect4.Server.Game;

public enum SeatStatus
{
    Empty,
    Online,
    Reconnecting
}

public sealed class SeatInfo
{
    public string Name { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public string? ConnectionId { get; set; }
    public SeatStatus Status { get; set; } = SeatStatus.Empty;
    public CancellationTokenSource? ForfeitCts { get; set; }

    public bool IsOccupied => Status != SeatStatus.Empty;
}

public sealed class RematchState
{
    public bool RedRequested { get; set; }
    public bool YellowRequested { get; set; }

    public bool BothAgreed => RedRequested && YellowRequested;

    public void Reset()
    {
        RedRequested = false;
        YellowRequested = false;
    }
}

public sealed class ScoreTracker
{
    public int RedWins { get; private set; }
    public int YellowWins { get; private set; }
    public int Draws { get; private set; }

    public void RecordWin(Player winner)
    {
        if (winner == Player.Red) RedWins++;
        else if (winner == Player.Yellow) YellowWins++;
    }

    public void RecordDraw() => Draws++;
}

/// <summary>
/// Server-side state for a single game room: board, seats, spectators, score, rematch.
/// All mutation must happen while holding <see cref="Lock"/>.
/// </summary>
public sealed class GameSession
{
    public required string Code { get; init; }
    public ConnectFourBoard Board { get; private set; } = new();
    public SeatInfo Red { get; } = new();
    public SeatInfo Yellow { get; } = new();
    public HashSet<string> SpectatorConnectionIds { get; } = new();
    public ScoreTracker Score { get; } = new();
    public RematchState Rematch { get; } = new();
    public SemaphoreSlim Lock { get; } = new(1, 1);
    public DateTime LastActivityUtc { get; set; } = DateTime.UtcNow;
    public Player LoserGoesFirst { get; set; } = Player.Red;

    public bool IsBotGame { get; set; }
    public Player BotSeat { get; set; } = Player.None;
    public BotDifficulty BotDifficulty { get; set; } = BotDifficulty.Medium;

    private const int MaxChatHistory = 100;
    private readonly List<ChatMessageDto> _chatHistory = new();
    public IReadOnlyList<ChatMessageDto> ChatHistory => _chatHistory;

    public void AddChatMessage(ChatMessageDto message)
    {
        _chatHistory.Add(message);
        if (_chatHistory.Count > MaxChatHistory)
        {
            _chatHistory.RemoveAt(0);
        }
    }

    public SeatInfo? GetSeat(Player player) => player switch
    {
        Player.Red => Red,
        Player.Yellow => Yellow,
        _ => null
    };

    public Player? GetPlayerByConnection(string connectionId)
    {
        if (Red.ConnectionId == connectionId) return Player.Red;
        if (Yellow.ConnectionId == connectionId) return Player.Yellow;
        return null;
    }

    public Player? GetPlayerByToken(string token)
    {
        if (!string.IsNullOrEmpty(token))
        {
            if (Red.Token == token) return Player.Red;
            if (Yellow.Token == token) return Player.Yellow;
        }
        return null;
    }

    public void ResetBoardForRematch()
    {
        Board = new ConnectFourBoard(LoserGoesFirst);
        Rematch.Reset();
    }
}
