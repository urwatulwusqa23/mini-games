using Connect4.Bot;
using Connect4.Engine;
using Connect4.Server.Models;
using Microsoft.AspNetCore.SignalR;

namespace Connect4.Server.Game;

/// <summary>
/// SignalR transport layer. Holds no game logic itself - all rules enforcement happens in
/// <see cref="ConnectFourBoard"/>, and all state mutation happens through <see cref="GameSession"/>.
/// </summary>
public sealed class GameHub : Hub
{
    private const int ReconnectWindowSeconds = 60;
    private const string BotTokenMarker = "__BOT__";
    private readonly GameManager _manager;
    private readonly ILogger<GameHub> _logger;

    public GameHub(GameManager manager, ILogger<GameHub> logger)
    {
        _manager = manager;
        _logger = logger;
    }

    public async Task CreateGame(string name)
    {
        var session = _manager.CreateGame();
        await session.Lock.WaitAsync();
        try
        {
            var token = Guid.NewGuid().ToString("N");
            session.Red.Name = string.IsNullOrWhiteSpace(name) ? "Player 1" : name.Trim();
            session.Red.Token = token;
            session.Red.ConnectionId = Context.ConnectionId;
            session.Red.Status = SeatStatus.Online;

            _manager.TrackConnection(Context.ConnectionId, session.Code);
            await Groups.AddToGroupAsync(Context.ConnectionId, session.Code);

            await Clients.Caller.SendAsync("GameCreated", new CreateGameResponse(session.Code, token, "Red"));
            await BroadcastState(session);
        }
        finally
        {
            session.Lock.Release();
        }
    }

    public async Task CreateBotGame(string name, string difficulty)
    {
        var session = _manager.CreateGame();
        await session.Lock.WaitAsync();
        try
        {
            var token = Guid.NewGuid().ToString("N");
            session.Red.Name = string.IsNullOrWhiteSpace(name) ? "Player 1" : name.Trim();
            session.Red.Token = token;
            session.Red.ConnectionId = Context.ConnectionId;
            session.Red.Status = SeatStatus.Online;

            session.IsBotGame = true;
            session.BotSeat = Player.Yellow;
            session.BotDifficulty = ParseDifficulty(difficulty);
            session.Yellow.Name = $"Bot ({session.BotDifficulty})";
            session.Yellow.Token = BotTokenMarker;
            session.Yellow.Status = SeatStatus.Online;

            _manager.TrackConnection(Context.ConnectionId, session.Code);
            await Groups.AddToGroupAsync(Context.ConnectionId, session.Code);

            await Clients.Caller.SendAsync("GameCreated", new CreateGameResponse(session.Code, token, "Red"));
            await BroadcastState(session);
        }
        finally
        {
            session.Lock.Release();
        }
    }

    public async Task JoinGame(string code, string name, string? token)
    {
        if (!_manager.TryGetGame(code, out var session))
        {
            await Clients.Caller.SendAsync("Error", new ErrorDto("Game not found."));
            return;
        }

        await session.Lock.WaitAsync();
        try
        {
            var normalizedName = string.IsNullOrWhiteSpace(name) ? "Player" : name.Trim();

            // Reconnect by token takes priority over fresh seat assignment.
            var existingSeat = string.IsNullOrEmpty(token) ? null : session.GetPlayerByToken(token);
            if (existingSeat is { } seatToRestore)
            {
                var seatInfo = session.GetSeat(seatToRestore)!;
                seatInfo.ConnectionId = Context.ConnectionId;
                seatInfo.Status = SeatStatus.Online;
                seatInfo.ForfeitCts?.Cancel();
                seatInfo.ForfeitCts = null;

                _manager.TrackConnection(Context.ConnectionId, session.Code);
                await Groups.AddToGroupAsync(Context.ConnectionId, session.Code);
                await Clients.Caller.SendAsync("JoinedGame", new JoinGameResponse(session.Code, seatInfo.Token, seatToRestore.ToString()));
                await Clients.Caller.SendAsync("ChatHistory", session.ChatHistory);
                await BroadcastState(session);
                await Clients.Group(session.Code).SendAsync("PlayerJoined", seatInfo.Name, seatToRestore.ToString());
                return;
            }

            SeatInfo? freeSeat = !session.Red.IsOccupied ? session.Red : !session.Yellow.IsOccupied ? session.Yellow : null;
            var seatPlayer = freeSeat == session.Red ? Player.Red : Player.Yellow;

            if (freeSeat is not null)
            {
                var newToken = Guid.NewGuid().ToString("N");
                freeSeat.Name = normalizedName;
                freeSeat.Token = newToken;
                freeSeat.ConnectionId = Context.ConnectionId;
                freeSeat.Status = SeatStatus.Online;

                _manager.TrackConnection(Context.ConnectionId, session.Code);
                await Groups.AddToGroupAsync(Context.ConnectionId, session.Code);
                await Clients.Caller.SendAsync("JoinedGame", new JoinGameResponse(session.Code, newToken, seatPlayer.ToString()));
                await Clients.Caller.SendAsync("ChatHistory", session.ChatHistory);
                await BroadcastState(session);
                await Clients.Group(session.Code).SendAsync("PlayerJoined", normalizedName, seatPlayer.ToString());
                return;
            }

            // Both seats taken: join as a spectator.
            session.SpectatorConnectionIds.Add(Context.ConnectionId);
            _manager.TrackConnection(Context.ConnectionId, session.Code);
            await Groups.AddToGroupAsync(Context.ConnectionId, session.Code);
            await Clients.Caller.SendAsync("JoinedGame", new JoinGameResponse(session.Code, string.Empty, "Spectator"));
            await Clients.Caller.SendAsync("ChatHistory", session.ChatHistory);
            await BroadcastState(session);
        }
        finally
        {
            session.Lock.Release();
        }
    }

    public async Task DropPiece(string code, int column)
    {
        if (!_manager.TryGetGame(code, out var session))
        {
            await Clients.Caller.SendAsync("Error", new ErrorDto("Game not found."));
            return;
        }

        await session.Lock.WaitAsync();
        try
        {
            var player = session.GetPlayerByConnection(Context.ConnectionId);
            if (player is null)
            {
                await Clients.Caller.SendAsync("Error", new ErrorDto("You are not a player in this game."));
                return;
            }

            if (session.IsBotGame && player.Value == session.BotSeat)
            {
                // The bot's seat has no real connection; a human can never legitimately hold it.
                await Clients.Caller.SendAsync("Error", new ErrorDto("You are not a player in this game."));
                return;
            }

            var result = session.Board.DropPiece(player.Value, column);
            if (result.Status != MoveStatus.Ok)
            {
                await Clients.Caller.SendAsync("Error", new ErrorDto(DescribeMoveStatus(result.Status)));
                return;
            }

            session.LastActivityUtc = DateTime.UtcNow;
            await BroadcastState(session);
            await HandleGameOverIfApplicable(session, result);

            if (!result.IsWin && !result.IsDraw)
            {
                await TriggerBotMoveIfNeeded(session);
            }
        }
        finally
        {
            session.Lock.Release();
        }
    }

    /// <summary>
    /// If this is a bot game and it's the bot's turn, computes and plays its move, then
    /// broadcasts the result. No-op otherwise. Caller must already hold <see cref="GameSession.Lock"/>.
    /// </summary>
    private async Task TriggerBotMoveIfNeeded(GameSession session)
    {
        if (!session.IsBotGame || session.Board.IsOver || session.Board.CurrentTurn != session.BotSeat)
        {
            return;
        }

        await Task.Delay(400); // brief "thinking" pause so the bot's move doesn't feel instant
        var botColumn = ConnectFourBot.ChooseColumn(session.Board, session.BotSeat, session.BotDifficulty);
        var botResult = session.Board.DropPiece(session.BotSeat, botColumn);
        await BroadcastState(session);
        await HandleGameOverIfApplicable(session, botResult);
    }

    private async Task HandleGameOverIfApplicable(GameSession session, MoveResult result)
    {
        if (result.IsWin)
        {
            session.Score.RecordWin(result.Winner);
            session.LoserGoesFirst = result.Winner == Player.Red ? Player.Yellow : Player.Red;
            await Clients.Group(session.Code).SendAsync(
                "GameOver",
                result.Winner.ToString(),
                result.WinningCells.Select(c => new CellDto(c.Row, c.Column)).ToArray());
        }
        else if (result.IsDraw)
        {
            session.Score.RecordDraw();
            await Clients.Group(session.Code).SendAsync("GameOver", (string?)null, Array.Empty<CellDto>());
        }
    }

    public async Task RequestRematch(string code)
    {
        if (!_manager.TryGetGame(code, out var session))
        {
            return;
        }

        await session.Lock.WaitAsync();
        try
        {
            var player = session.GetPlayerByConnection(Context.ConnectionId);
            if (player is null || !session.Board.IsOver)
            {
                return;
            }

            if (player == Player.Red) session.Rematch.RedRequested = true;
            else session.Rematch.YellowRequested = true;

            // The bot always agrees to a rematch immediately - there's no second human to ask.
            if (session.IsBotGame)
            {
                if (session.BotSeat == Player.Red) session.Rematch.RedRequested = true;
                else session.Rematch.YellowRequested = true;
            }

            await Clients.Group(session.Code).SendAsync("RematchRequested", player.Value.ToString());

            if (session.Rematch.BothAgreed)
            {
                session.ResetBoardForRematch();
                await BroadcastState(session);
                await TriggerBotMoveIfNeeded(session);
            }
        }
        finally
        {
            session.Lock.Release();
        }
    }

    public async Task SendChatMessage(string code, string text)
    {
        if (!_manager.TryGetGame(code, out var session))
        {
            return;
        }

        var trimmed = (text ?? string.Empty).Trim();
        if (trimmed.Length == 0)
        {
            return;
        }
        if (trimmed.Length > 500)
        {
            trimmed = trimmed[..500];
        }

        await session.Lock.WaitAsync();
        try
        {
            var sender = ResolveSender(session, Context.ConnectionId);
            if (sender is null)
            {
                return; // not a recognized participant (e.g. already disconnected)
            }

            var message = new ChatMessageDto(sender.Value.Name, sender.Value.Seat, trimmed, DateTime.UtcNow);
            session.AddChatMessage(message);
            await Clients.Group(session.Code).SendAsync("ChatMessageReceived", message);
        }
        finally
        {
            session.Lock.Release();
        }
    }

    private (string Name, string Seat)? ResolveSender(GameSession session, string connectionId)
    {
        var player = session.GetPlayerByConnection(connectionId);
        if (player is { } seat)
        {
            var seatInfo = session.GetSeat(seat)!;
            return (seatInfo.Name, seat.ToString());
        }

        if (session.SpectatorConnectionIds.Contains(connectionId))
        {
            return ("Spectator", "Spectator");
        }

        return null;
    }

    public async Task LeaveGame(string code)
    {
        if (!_manager.TryGetGame(code, out var session))
        {
            return;
        }

        await RemoveConnectionFromSession(session, Context.ConnectionId, forfeitImmediately: true);
        _manager.UntrackConnection(Context.ConnectionId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, session.Code);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (_manager.TryGetGameForConnection(Context.ConnectionId, out var code) && _manager.TryGetGame(code, out var session))
        {
            await RemoveConnectionFromSession(session, Context.ConnectionId, forfeitImmediately: false);
        }
        _manager.UntrackConnection(Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    private async Task RemoveConnectionFromSession(GameSession session, string connectionId, bool forfeitImmediately)
    {
        await session.Lock.WaitAsync();
        Player? disconnectedPlayer = null;
        try
        {
            if (session.SpectatorConnectionIds.Remove(connectionId))
            {
                await BroadcastState(session);
                return;
            }

            disconnectedPlayer = session.GetPlayerByConnection(connectionId);
            if (disconnectedPlayer is null)
            {
                return;
            }

            var seat = session.GetSeat(disconnectedPlayer.Value)!;
            seat.ConnectionId = null;

            if (session.Board.IsOver || forfeitImmediately)
            {
                seat.Status = SeatStatus.Empty;
                seat.Token = string.Empty;
                seat.Name = string.Empty;
            }
            else
            {
                seat.Status = SeatStatus.Reconnecting;
                seat.ForfeitCts = new CancellationTokenSource();
                _ = ScheduleForfeit(session, disconnectedPlayer.Value, seat.ForfeitCts.Token);
            }

            await Clients.Group(session.Code).SendAsync("PlayerLeft", disconnectedPlayer.Value.ToString(), forfeitImmediately);
            await BroadcastState(session);
        }
        finally
        {
            session.Lock.Release();
        }
    }

    private async Task ScheduleForfeit(GameSession session, Player disconnectedPlayer, CancellationToken token)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(ReconnectWindowSeconds), token);
        }
        catch (TaskCanceledException)
        {
            return;
        }

        await session.Lock.WaitAsync();
        try
        {
            var seat = session.GetSeat(disconnectedPlayer)!;
            if (seat.Status != SeatStatus.Reconnecting || token.IsCancellationRequested)
            {
                return;
            }

            seat.Status = SeatStatus.Empty;
            seat.Token = string.Empty;
            seat.Name = string.Empty;

            if (!session.Board.IsOver)
            {
                var winner = disconnectedPlayer == Player.Red ? Player.Yellow : Player.Red;
                session.Score.RecordWin(winner);
                await Clients.Group(session.Code).SendAsync("GameOver", winner.ToString(), Array.Empty<CellDto>());
            }

            await BroadcastState(session);
        }
        finally
        {
            session.Lock.Release();
        }
    }

    private async Task BroadcastState(GameSession session)
    {
        await Clients.Group(session.Code).SendAsync("GameStateUpdated", BuildStateDto(session));
    }

    private static GameStateDto BuildStateDto(GameSession session)
    {
        var snapshot = session.Board.Snapshot();
        var board = new int[ConnectFourBoard.Rows][];
        for (int r = 0; r < ConnectFourBoard.Rows; r++)
        {
            board[r] = new int[ConnectFourBoard.Columns];
            for (int c = 0; c < ConnectFourBoard.Columns; c++)
            {
                board[r][c] = (int)snapshot[r, c];
            }
        }

        return new GameStateDto(
            Code: session.Code,
            Board: board,
            CurrentTurn: session.Board.CurrentTurn.ToString(),
            IsOver: session.Board.IsOver,
            Winner: session.Board.Winner == Player.None ? null : session.Board.Winner.ToString(),
            IsDraw: session.Board.IsOver && session.Board.Winner == Player.None,
            WinningCells: session.Board.WinningCells.Select(c => new CellDto(c.Row, c.Column)).ToArray(),
            Red: session.Red.IsOccupied ? new PlayerInfoDto(session.Red.Name, session.Red.Status.ToString()) : null,
            Yellow: session.Yellow.IsOccupied ? new PlayerInfoDto(session.Yellow.Name, session.Yellow.Status.ToString()) : null,
            SpectatorCount: session.SpectatorConnectionIds.Count,
            Score: new ScoreDto(session.Score.RedWins, session.Score.YellowWins, session.Score.Draws),
            Rematch: new RematchStateDto(session.Rematch.RedRequested, session.Rematch.YellowRequested));
    }

    private static BotDifficulty ParseDifficulty(string? difficulty) =>
        Enum.TryParse<BotDifficulty>(difficulty, ignoreCase: true, out var parsed) ? parsed : BotDifficulty.Medium;

    private static string DescribeMoveStatus(MoveStatus status) => status switch
    {
        MoveStatus.WrongTurn => "It's not your turn.",
        MoveStatus.ColumnFull => "That column is full.",
        MoveStatus.InvalidColumn => "Invalid column.",
        MoveStatus.GameAlreadyOver => "The game is already over.",
        _ => "Invalid move."
    };
}
