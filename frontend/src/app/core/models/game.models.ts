export type Seat = 'Red' | 'Yellow' | 'Spectator';

export type ConnectionState = 'Online' | 'Reconnecting' | 'Empty';

export interface PlayerInfo {
  name: string;
  status: ConnectionState;
}

export interface Cell {
  row: number;
  column: number;
}

export interface Score {
  redWins: number;
  yellowWins: number;
  draws: number;
}

export interface RematchState {
  redRequested: boolean;
  yellowRequested: boolean;
}

export interface GameState {
  code: string;
  board: number[][];
  currentTurn: 'Red' | 'Yellow';
  isOver: boolean;
  winner: 'Red' | 'Yellow' | null;
  isDraw: boolean;
  winningCells: Cell[];
  red: PlayerInfo | null;
  yellow: PlayerInfo | null;
  spectatorCount: number;
  score: Score;
  rematch: RematchState;
}

export interface CreateGameResponse {
  code: string;
  token: string;
  seat: string;
}

export interface JoinGameResponse {
  code: string;
  token: string;
  seat: string;
}

export interface ErrorPayload {
  message: string;
}

export interface SeatToken {
  code: string;
  token: string;
  seat: Seat;
  name: string;
}
