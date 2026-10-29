import { Location } from '@angular/common';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { routes } from './app.routes';
import { BoardsStore } from './boards/data-access/boards.store';
import { BoardDetailComponent } from './boards/feature/board-detail/board-detail.component';
import { BoardListComponent } from './boards/feature/board-list/board-list.component';
import { LabelsStore } from './labels/data-access/labels.store';
import { TasksStore } from './tasks/data-access/tasks.store';

describe('app routes', () => {
  let harness: RouterTestingHarness;
  let location: Location;

  beforeEach(async () => {
    // BoardListComponent now injects the real store (which needs BOARD_CLIENT); this spec only
    // tests routing, so a no-op stub keeps it away from the network.
    const boardsStoreStub = {
      entities: signal([]),
      entityMap: signal({}),
      loading: signal(false),
      error: signal(null),
      loadBoards: () => undefined
    };
    // The detail page also drives the task board; stub both of its stores for the same reason.
    const tasksStoreStub = {
      entities: signal([]),
      filter: signal({ priority: null, labelId: null, text: '' }),
      todo: signal([]),
      inProgress: signal([]),
      done: signal([]),
      hasActiveFilter: signal(false),
      loading: signal(false),
      error: signal(null),
      loadTasks: () => undefined
    };
    const labelsStoreStub = { entities: signal([]), error: signal(null), loadLabels: () => undefined };
    TestBed.configureTestingModule({
      providers: [
        provideRouter(routes),
        { provide: BoardsStore, useValue: boardsStoreStub },
        { provide: TasksStore, useValue: tasksStoreStub },
        { provide: LabelsStore, useValue: labelsStoreStub }
      ]
    });
    harness = await RouterTestingHarness.create();
    location = TestBed.inject(Location);
  });

  it('should redirect the empty path to the boards list', async () => {
    const component = await harness.navigateByUrl('/');

    expect(location.path()).toBe('/boards');
    expect(component).toBeInstanceOf(BoardListComponent);
  });

  it('should lazy-load the detail page for /boards/:id', async () => {
    const component = await harness.navigateByUrl('/boards/5');

    expect(location.path()).toBe('/boards/5');
    expect(component).toBeInstanceOf(BoardDetailComponent);
  });

  it('should send unknown URLs to the boards list instead of a blank page', async () => {
    const component = await harness.navigateByUrl('/no/such/page');

    expect(location.path()).toBe('/boards');
    expect(component).toBeInstanceOf(BoardListComponent);
  });
});
