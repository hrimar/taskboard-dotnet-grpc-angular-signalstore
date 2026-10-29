import { ChangeDetectionStrategy, Component } from '@angular/core';

// Placeholder so the /boards/:id route compiles; it becomes the board page (and later the
// Kanban host) in a following step.
@Component({
  selector: 'app-board-detail',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<h1>Board</h1>`
})
export class BoardDetailComponent {}
