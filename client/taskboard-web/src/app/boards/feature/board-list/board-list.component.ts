import { ChangeDetectionStrategy, Component, inject, OnInit } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatDialog } from '@angular/material/dialog';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { ConfirmDialogComponent, ConfirmDialogData } from '../../../shared/ui/confirm-dialog/confirm-dialog.component';
import type { Board } from '../../../core/proto-gen/board';
import { toBoardChanges } from '../../data-access/board-changes';
import { BoardsStore } from '../../data-access/boards.store';
import {
  BoardFormDialogComponent,
  BoardFormDialogData,
  BoardFormResult
} from '../../ui/board-form-dialog/board-form-dialog.component';

@Component({
  selector: 'app-board-list',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, MatButtonModule, MatCardModule, MatProgressBarModule],
  templateUrl: './board-list.component.html',
  styleUrl: './board-list.component.scss'
})
export class BoardListComponent implements OnInit {
  protected readonly store = inject(BoardsStore);
  private readonly dialog = inject(MatDialog);

  ngOnInit(): void {
    // The store is root-provided and survives navigation, so cached entities are shown
    // immediately while this refresh runs (the loading bar only appears for an empty list).
    this.store.loadBoards();
  }

  protected createBoard(): void {
    this.dialog
      .open<BoardFormDialogComponent, BoardFormDialogData, BoardFormResult>(BoardFormDialogComponent, { data: {} })
      .afterClosed()
      .subscribe((result) => {
        if (!result) return;
        // description is a proto3 `optional`: omit it rather than send an empty string.
        this.store.createBoard({ name: result.name, ...(result.description ? { description: result.description } : {}) });
      });
  }

  protected editBoard(board: Board): void {
    this.dialog
      .open<BoardFormDialogComponent, BoardFormDialogData, BoardFormResult>(BoardFormDialogComponent, { data: { board } })
      .afterClosed()
      .subscribe((result) => {
        if (!result) return;
        const changes = toBoardChanges(board, result);
        if (changes) this.store.updateBoard({ id: board.id, ...changes });
      });
  }

  protected deleteBoard(board: Board): void {
    this.dialog
      .open<ConfirmDialogComponent, ConfirmDialogData, boolean>(ConfirmDialogComponent, {
        data: {
          title: 'Delete board',
          message: `Delete "${board.name}"? This cannot be undone.`,
          confirmLabel: 'Delete'
        }
      })
      .afterClosed()
      .subscribe((confirmed) => {
        // Backdrop click / Esc yields undefined, so only an explicit true deletes.
        if (confirmed === true) this.store.deleteBoard({ id: board.id });
      });
  }
}
