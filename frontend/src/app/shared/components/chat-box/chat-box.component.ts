import {
  AfterViewChecked,
  Component,
  ElementRef,
  EventEmitter,
  Input,
  Output,
  ViewChild
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ChatMessage, Seat } from '../../../core/models/game.models';

@Component({
  selector: 'app-chat-box',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './chat-box.component.html',
  styleUrl: './chat-box.component.scss'
})
export class ChatBoxComponent implements AfterViewChecked {
  @Input() messages: ChatMessage[] = [];
  @Input() mySeat: Seat | null = null;
  @Output() sendMessage = new EventEmitter<string>();

  @ViewChild('scrollArea') private scrollArea?: ElementRef<HTMLDivElement>;

  draft = '';
  private lastRenderedCount = 0;

  send(): void {
    const text = this.draft.trim();
    if (!text) {
      return;
    }
    this.sendMessage.emit(text);
    this.draft = '';
  }

  isMine(message: ChatMessage): boolean {
    return this.mySeat !== null && message.seat === this.mySeat;
  }

  formatTime(iso: string): string {
    const date = new Date(iso);
    if (Number.isNaN(date.getTime())) {
      return '';
    }
    return date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
  }

  trackByIndex(index: number): number {
    return index;
  }

  ngAfterViewChecked(): void {
    if (this.messages.length !== this.lastRenderedCount) {
      this.lastRenderedCount = this.messages.length;
      const el = this.scrollArea?.nativeElement;
      if (el) {
        el.scrollTop = el.scrollHeight;
      }
    }
  }
}
