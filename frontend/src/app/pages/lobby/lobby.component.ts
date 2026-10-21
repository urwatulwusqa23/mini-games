import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { GameService } from '../../core/services/game.service';

@Component({
  selector: 'app-lobby',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './lobby.component.html',
  styleUrl: './lobby.component.scss'
})
export class LobbyComponent {
  private readonly game = inject(GameService);
  private readonly router = inject(Router);

  name = '';
  joinCode = '';

  readonly busy = signal(false);
  readonly errorMessage = signal<string | null>(null);

  async createGame(): Promise<void> {
    if (!this.name.trim()) {
      this.errorMessage.set('Enter a display name first.');
      return;
    }

    this.busy.set(true);
    this.errorMessage.set(null);
    try {
      const code = await this.game.createGame(this.name.trim());
      await this.router.navigate(['/game', code]);
    } catch (err) {
      this.errorMessage.set('Could not create a game. Is the server running?');
    } finally {
      this.busy.set(false);
    }
  }

  async joinGame(): Promise<void> {
    if (!this.name.trim()) {
      this.errorMessage.set('Enter a display name first.');
      return;
    }
    if (!this.joinCode.trim()) {
      this.errorMessage.set('Enter a room code first.');
      return;
    }

    this.busy.set(true);
    this.errorMessage.set(null);
    try {
      const code = this.joinCode.trim().toUpperCase();
      await this.game.joinGame(code, this.name.trim());
      await this.router.navigate(['/game', code]);
    } catch (err) {
      this.errorMessage.set('Could not join that game. Check the code and try again.');
    } finally {
      this.busy.set(false);
    }
  }
}
