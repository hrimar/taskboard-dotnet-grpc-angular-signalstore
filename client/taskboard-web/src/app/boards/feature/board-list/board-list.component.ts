import { ChangeDetectionStrategy, Component } from '@angular/core';

// Placeholder so the /boards route compiles; the real list (Material cards, store-driven
// loading/error/empty states, create/edit/delete) replaces it in the next steps.
@Component({
  selector: 'app-board-list',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<h1>Boards</h1>`
})
export class BoardListComponent {}
