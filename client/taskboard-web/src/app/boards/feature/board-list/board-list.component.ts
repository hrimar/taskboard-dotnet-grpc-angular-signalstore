import { ChangeDetectionStrategy, Component, inject, OnInit } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { BoardsStore } from '../../data-access/boards.store';

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

  ngOnInit(): void {
    // The store is root-provided and survives navigation, so cached entities are shown
    // immediately while this refresh runs (the loading bar only appears for an empty list).
    this.store.loadBoards();
  }
}
