import { Routes } from '@angular/router';

// Feature pages are lazy-loaded: the browser downloads a page's code only when its route is
// first visited, which keeps the initial bundle small as more features (tasks, labels) arrive.
export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'boards' },
  {
    path: 'boards',
    loadComponent: () =>
      import('./boards/feature/board-list/board-list.component').then((m) => m.BoardListComponent)
  },
  {
    path: 'boards/:id',
    loadComponent: () =>
      import('./boards/feature/board-detail/board-detail.component').then((m) => m.BoardDetailComponent)
  },
  // Unknown URLs land on the list instead of a blank page.
  { path: '**', redirectTo: 'boards' }
];
