import { Injectable, signal } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import {
  CreateGameResponse,
  ErrorPayload,
  GameState,
  JoinGameResponse,
  Seat,
  SeatToken
} from '../models/game.models';
import { environment } from '../../../environments/environment';

const STORAGE_KEY_PREFIX = 'connect4.seat.';

@Injectable({ providedIn: 'root' })
export class GameService {
  private connection: signalR.HubConnection | null = null;

  readonly gameState = signal<GameState | null>(null);
  readonly mySeat = signal<Seat | null>(null);
  readonly myName = signal<string>('');
  readonly connectionStatus = signal<'disconnected' | 'connecting' | 'connected'>('disconnected');
  readonly lastError = signal<string | null>(null);
  readonly lastEvent = signal<{ type: string; payload?: unknown } | null>(null);

  private async ensureConnected(): Promise<signalR.HubConnection> {
    if (this.connection && this.connection.state === signalR.HubConnectionState.Connected) {
      return this.connection;
    }

    if (!this.connection) {
      this.connection = new signalR.HubConnectionBuilder()
        .withUrl(environment.hubUrl)
        .withAutomaticReconnect()
        .build();

      this.registerHandlers(this.connection);
    }

    if (this.connection.state === signalR.HubConnectionState.Disconnected) {
      this.connectionStatus.set('connecting');
      await this.connection.start();
      this.connectionStatus.set('connected');
    }

    return this.connection;
  }

  private registerHandlers(connection: signalR.HubConnection): void {
    connection.on('GameStateUpdated', (state: GameState) => {
      this.gameState.set(state);
    });

    connection.on('PlayerJoined', (name: string, seat: string) => {
      this.lastEvent.set({ type: 'PlayerJoined', payload: { name, seat } });
    });

    connection.on('PlayerLeft', (seat: string, forfeited: boolean) => {
      this.lastEvent.set({ type: 'PlayerLeft', payload: { seat, forfeited } });
    });

    connection.on('GameOver', (winner: string | null, winningCells: unknown) => {
      this.lastEvent.set({ type: 'GameOver', payload: { winner, winningCells } });
    });

    connection.on('RematchRequested', (seat: string) => {
      this.lastEvent.set({ type: 'RematchRequested', payload: { seat } });
    });

    connection.on('Error', (error: ErrorPayload) => {
      this.lastError.set(error.message);
    });

    connection.onreconnecting(() => this.connectionStatus.set('connecting'));
    connection.onreconnected(() => this.connectionStatus.set('connected'));
    connection.onclose(() => this.connectionStatus.set('disconnected'));
  }

  async createGame(name: string): Promise<string> {
    const connection = await this.ensureConnected();
    this.myName.set(name);

    return new Promise<string>((resolve, reject) => {
      const handler = (response: CreateGameResponse) => {
        connection.off('GameCreated', handler);
        this.mySeat.set('Red');
        this.saveSeatToken({ code: response.code, token: response.token, seat: 'Red', name });
        resolve(response.code);
      };
      connection.on('GameCreated', handler);
      connection.invoke('CreateGame', name).catch(reject);
    });
  }

  async joinGame(code: string, name: string): Promise<void> {
    const connection = await this.ensureConnected();
    this.myName.set(name);
    const normalizedCode = code.trim().toUpperCase();
    const existing = this.loadSeatToken(normalizedCode);

    return new Promise<void>((resolve, reject) => {
      const handler = (response: JoinGameResponse) => {
        connection.off('JoinedGame', handler);
        const seat = response.seat as Seat;
        this.mySeat.set(seat);
        if (seat !== 'Spectator') {
          this.saveSeatToken({ code: response.code, token: response.token, seat, name });
        }
        resolve();
      };
      connection.on('JoinedGame', handler);
      connection.invoke('JoinGame', normalizedCode, name, existing?.token ?? null).catch(reject);
    });
  }

  async dropPiece(code: string, column: number): Promise<void> {
    const connection = await this.ensureConnected();
    await connection.invoke('DropPiece', code, column);
  }

  async requestRematch(code: string): Promise<void> {
    const connection = await this.ensureConnected();
    await connection.invoke('RequestRematch', code);
  }

  async leaveGame(code: string): Promise<void> {
    if (!this.connection) {
      return;
    }
    await this.connection.invoke('LeaveGame', code);
    this.clearSeatToken(code);
    this.gameState.set(null);
    this.mySeat.set(null);
  }

  private saveSeatToken(seatToken: SeatToken): void {
    try {
      localStorage.setItem(STORAGE_KEY_PREFIX + seatToken.code, JSON.stringify(seatToken));
    } catch {
      // localStorage unavailable (e.g. private browsing); reconnection by token will be skipped.
    }
  }

  loadSeatToken(code: string): SeatToken | null {
    try {
      const raw = localStorage.getItem(STORAGE_KEY_PREFIX + code.trim().toUpperCase());
      return raw ? (JSON.parse(raw) as SeatToken) : null;
    } catch {
      return null;
    }
  }

  private clearSeatToken(code: string): void {
    try {
      localStorage.removeItem(STORAGE_KEY_PREFIX + code.trim().toUpperCase());
    } catch {
      // ignore
    }
  }
}
