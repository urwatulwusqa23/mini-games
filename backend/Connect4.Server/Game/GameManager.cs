using System.Collections.Concurrent;
using Connect4.Engine;

namespace Connect4.Server.Game;

public enum JoinOutcome
{
    CreatedAsPlayer,
    JoinedAsPlayer,
    Reconnected,
    JoinedAsSpectator,
    NotFound,
    InvalidToken
}

public sealed record JoinResult(JoinOutcome Outcome, GameSession? Session, Player Seat, string Token);

/// <summary>
/// In-memory store of all active game rooms, keyed by room code. Thread-safe for concurrent
/// access across SignalR connections; per-game mutations are additionally serialized via
/// each <see cref="GameSession.Lock"/>.
/// </summary>
public sealed class GameManager
{
    private readonly ConcurrentDictionary<string, GameSession> _sessions = new();
    private readonly ConcurrentDictionary<string, string> _connectionToGame = new();
    private static readonly char[] CodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789".ToCharArray();

    public void TrackConnection(string connectionId, string gameCode) =>
        _connectionToGame[connectionId] = gameCode;

    public bool TryGetGameForConnection(string connectionId, out string gameCode) =>
        _connectionToGame.TryGetValue(connectionId, out gameCode!);

    public void UntrackConnection(string connectionId) =>
        _connectionToGame.TryRemove(connectionId, out _);

    public GameSession CreateGame()
    {
        GameSession session;
        string code;
        do
        {
            code = GenerateCode();
            session = new GameSession { Code = code };
        } while (!_sessions.TryAdd(code, session));

        return session;
    }

    public bool TryGetGame(string code, out GameSession session)
    {
        return _sessions.TryGetValue(NormalizeCode(code), out session!);
    }

    public void RemoveGame(string code)
    {
        _sessions.TryRemove(NormalizeCode(code), out _);
    }

    public static string NormalizeCode(string code) => code.Trim().ToUpperInvariant();

    private static string GenerateCode()
    {
        var buffer = new char[6];
        for (int i = 0; i < buffer.Length; i++)
        {
            buffer[i] = CodeAlphabet[Random.Shared.Next(CodeAlphabet.Length)];
        }
        return new string(buffer);
    }
}
