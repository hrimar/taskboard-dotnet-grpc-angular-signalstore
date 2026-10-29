import { ChangeDetectionStrategy, Component, computed, inject, OnInit } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { map } from 'rxjs';
import { BoardsStore } from '../../data-access/boards.store';

@Component({
  selector: 'app-board-detail',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, MatButtonModule, MatProgressBarModule],
  templateUrl: './board-detail.component.html',
  styleUrl: './board-detail.component.scss'
})
export class BoardDetailComponent implements OnInit {
  protected readonly store = inject(BoardsStore);

  // ActivatedRoute instead of withComponentInputBinding(): it needs no global router config,
  // so the component works the same under any provideRouter(...) (including the routing spec),
  // and paramMap keeps the page in sync if the id changes while the component is reused.
  private readonly id = toSignal(
    inject(ActivatedRoute).paramMap.pipe(map((params) => Number(params.get('id')))),
    { requireSync: true }
  );

  protected readonly board = computed(() => this.store.entityMap()[this.id()]);

  ngOnInit(): void {
    // Deep link / page refresh: the store is empty, so fetch. When boards are already cached
    // (we came from the list) no extra request is needed.
    if (this.store.entities().length === 0) {
      this.store.loadBoards();
    }
  }
}
