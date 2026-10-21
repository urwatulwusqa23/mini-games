import { Component, EventEmitter, Input, Output, signal } from '@angular/core';
import { Cell } from '../../../core/models/game.models';

type CellOwner = 0 | 1 | 2; // 0 = empty, 1 = Red, 2 = Yellow

@Component({
  selector: 'app-board',
  standalone: true,
  imports: [],
  templateUrl: './board.component.html',
  styleUrl: './board.component.scss'
})
export class BoardComponent {
  @Input() board: number[][] = [];
  @Input() winningCells: Cell[] = [];
  @Input() disabled = false;
  @Input() myTurnColumn: 1 | 2 | null = null; // which color the hovering player would drop

  @Output() columnClicked = new EventEmitter<number>();

  readonly hoveredColumn = signal<number | null>(null);

  readonly columns = Array.from({ length: 7 }, (_, i) => i);
  readonly rows = Array.from({ length: 6 }, (_, i) => i);

  onColumnEnter(column: number): void {
    if (!this.disabled) {
      this.hoveredColumn.set(column);
    }
  }

  onColumnLeave(): void {
    this.hoveredColumn.set(null);
  }

  onColumnClick(column: number): void {
    if (this.disabled) {
      return;
    }
    this.columnClicked.emit(column);
  }

  cellOwner(row: number, column: number): CellOwner {
    return (this.board[row]?.[column] ?? 0) as CellOwner;
  }

  isWinningCell(row: number, column: number): boolean {
    return this.winningCells.some(c => c.row === row && c.column === column);
  }

  isColumnFull(column: number): boolean {
    return this.board.every(row => row[column] !== 0);
  }

  trackByIndex(index: number): number {
    return index;
  }
}
