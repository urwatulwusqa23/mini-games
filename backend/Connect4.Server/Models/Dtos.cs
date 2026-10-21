using Connect4.Engine;

namespace Connect4.Server.Models;

public sealed record PlayerDto(string ConnectionState, string Name, string Seat);

public sealed record GameStateDto(
    string Code,
    int[][] Board,
    string CurrentTurn,
    bool IsOver,
    string? Winner,
    bool IsDraw,
    CellDto[] WinningCells,
    PlayerInfoDto? Red,
    PlayerInfoDto? Yellow,
    int SpectatorCount,
    ScoreDto Score,
    RematchStateDto Rematch);

public sealed record PlayerInfoDto(string Name, string Status);

public sealed record CellDto(int Row, int Column);

public sealed record ScoreDto(int RedWins, int YellowWins, int Draws);

public sealed record RematchStateDto(bool RedRequested, bool YellowRequested);

public sealed record CreateGameRequest(string Name);

public sealed record JoinGameRequest(string Code, string Name, string? Token);

public sealed record CreateGameResponse(string Code, string Token, string Seat);

public sealed record JoinGameResponse(string Code, string Token, string Seat);

public sealed record ErrorDto(string Message);
