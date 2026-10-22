import { Component, computed, inject, OnDestroy, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { BoardComponent } from '../../shared/components/board/board.component';
import { ChatBoxComponent } from '../../shared/components/chat-box/chat-box.component';
import { GameService } from '../../core/services/game.service';

@Component({
  selector: 'app-game',
  standalone: true,
  imports: [BoardComponent, ChatBoxComponent, FormsModule],
  templateUrl: './game.component.html',
  styleUrl: './game.component.scss'
})
export class GameComponent implements OnInit, OnDestroy {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  readonly game = inject(GameService);

  code = '';
  nameInput = '';

  readonly needsName = signal(false);
  readonly joining = signal(false);
  readonly joinError = signal<string | null>(null);

  readonly state = this.game.gameState;
  readonly mySeat = this.game.mySeat;
  readonly chatMessages = this.game.chatMessages;

  readonly mySeatColor = computed<1 | 2 | null>(() => {
    const seat = this.mySeat();
    return seat === 'Red' ? 1 : seat === 'Yellow' ? 2 : null;
  });

  readonly isMyTurn = computed(() => {
    const s = this.state();
    const seat = this.mySeat();
    return !!s && !s.isOver && (seat === s.currentTurn);
  });

  readonly canPlay = computed(() => {
    const seat = this.mySeat();
    return seat === 'Red' || seat === 'Yellow';
  });

  readonly turnLabel = computed(() => {
    const s = this.state();
    if (!s) return '';
    if (s.isOver) return '';
    return this.isMyTurn() ? "Your turn" : "Opponent's turn";
  });

  readonly resultLabel = computed(() => {
    const s = this.state();
    if (!s || !s.isOver) return null;
    if (s.isDraw) return "It's a draw!";
    if (s.winner === this.mySeat()) return 'You win!';
    if (s.winner) return `${s.winner} wins!`;
    return null;
  });

  async ngOnInit(): Promise<void> {
    this.code = this.route.snapshot.paramMap.get('code') ?? '';

    const existingToken = this.game.loadSeatToken(this.code);
    const alreadyInThisGame = this.state()?.code === this.code && this.mySeat();

    if (alreadyInThisGame) {
      return;
    }

    if (existingToken) {
      await this.attemptJoin(existingToken.name);
      return;
    }

    this.needsName.set(true);
  }

  ngOnDestroy(): void {
    // Intentionally do not call leaveGame here: a page refresh or navigation away should
    // not forfeit an in-progress match. Disconnect handling + the 60s reconnect window on
    // the server covers that case instead.
  }

  async joinWithName(): Promise<void> {
    if (!this.nameInput.trim()) {
      this.joinError.set('Enter a display name first.');
      return;
    }
    await this.attemptJoin(this.nameInput.trim());
  }

  private async attemptJoin(name: string): Promise<void> {
    this.joining.set(true);
    this.joinError.set(null);
    try {
      await this.game.joinGame(this.code, name);
      this.needsName.set(false);
    } catch {
      this.joinError.set('Could not join this game. Check the room code and try again.');
    } finally {
      this.joining.set(false);
    }
  }

  async drop(column: number): Promise<void> {
    if (!this.canPlay() || !this.isMyTurn()) {
      return;
    }
    await this.game.dropPiece(this.code, column);
  }

  readonly iRequestedRematch = computed(() => {
    const s = this.state();
    const seat = this.mySeat();
    if (!s) return false;
    return (seat === 'Red' && s.rematch.redRequested) || (seat === 'Yellow' && s.rematch.yellowRequested);
  });

  readonly opponentRequestedRematch = computed(() => {
    const s = this.state();
    const seat = this.mySeat();
    if (!s) return false;
    return (seat === 'Red' && s.rematch.yellowRequested) || (seat === 'Yellow' && s.rematch.redRequested);
  });

  async requestRematch(): Promise<void> {
    await this.game.requestRematch(this.code);
  }

  async leave(): Promise<void> {
    await this.game.leaveGame(this.code);
    await this.router.navigate(['/']);
  }

  async sendChatMessage(text: string): Promise<void> {
    await this.game.sendChatMessage(this.code, text);
  }

  copyInviteLink(): void {
    const url = `${window.location.origin}/game/${this.code}`;
    navigator.clipboard?.writeText(url).catch(() => {
      // Clipboard access may be blocked; the code is still visible on screen.
    });
  }
}
