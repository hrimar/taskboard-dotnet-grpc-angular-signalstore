import { ChangeDetectionStrategy, Component, input, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { LabelsStore } from '../../../labels/data-access/labels.store';
import { TasksStore } from '../../../tasks/data-access/tasks.store';
import { TaskBoardComponent } from '../../../tasks/feature/task-board/task-board.component';
import { BoardsStore } from '../../data-access/boards.store';
import type { Board } from '../../../core/proto-gen/board';
import { BoardDetailComponent } from './board-detail.component';

// The real board pulls in both task stores and the gRPC clients; this spec is about the page shell.
@Component({
  selector: 'app-task-board',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: 'Task board stub for {{ boardId() }}'
})
class TaskBoardStubComponent {
  readonly boardId = input.required<number>();
}

describe('BoardDetailComponent', () => {
  const entities = signal<Board[]>([]);
  const entityMap = signal<Record<number, Board>>({});
  const loading = signal(false);
  const error = signal<string | null>(null);
  let loadBoards: jasmine.Spy;
  let loadTasks: jasmine.Spy;
  let loadLabels: jasmine.Spy;

  function setBoards(boards: Board[]) {
    entities.set(boards);
    entityMap.set(Object.fromEntries(boards.map((b) => [b.id, b])));
  }

  function create(id = '1') {
    TestBed.configureTestingModule({
      imports: [BoardDetailComponent],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        { provide: ActivatedRoute, useValue: { paramMap: of(convertToParamMap({ id })) } },
        { provide: BoardsStore, useValue: { entities, entityMap, loading, error, loadBoards } },
        { provide: TasksStore, useValue: { loadTasks } },
        { provide: LabelsStore, useValue: { loadLabels } }
      ]
    }).overrideComponent(BoardDetailComponent, {
      remove: { imports: [TaskBoardComponent] },
      add: { imports: [TaskBoardStubComponent] }
    });
    const fixture = TestBed.createComponent(BoardDetailComponent);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  beforeEach(() => {
    setBoards([]);
    loading.set(false);
    error.set(null);
    loadBoards = jasmine.createSpy('loadBoards');
    loadTasks = jasmine.createSpy('loadTasks');
    loadLabels = jasmine.createSpy('loadLabels');
  });

  it('shows the board, a back link and the task board when found, without reloading', () => {
    setBoards([{ id: 1, name: 'Alpha', description: 'First board' } as Board]);
    const el = create('1');

    expect(el.querySelector('h1')!.textContent).toContain('Alpha');
    expect(el.textContent).toContain('First board');
    expect(el.textContent).toContain('Task board stub for 1');
    expect(loadTasks).toHaveBeenCalledWith(1);
    expect(loadLabels).toHaveBeenCalledTimes(1);
    expect(el.querySelector('a')!.getAttribute('href')).toBe('/boards');
    expect(loadBoards).not.toHaveBeenCalled();
  });

  it('shows "Board not found" with a link back when loaded but the id does not exist', () => {
    setBoards([{ id: 1, name: 'Alpha', description: '' } as Board]);
    const el = create('99');

    expect(el.textContent).toContain('Board not found');
    expect(el.querySelector('a')!.getAttribute('href')).toBe('/boards');
  });

  it('triggers loadBoards when the store is empty and shows a loading bar instead of "not found"', () => {
    loadBoards.and.callFake(() => loading.set(true));
    const el = create('1');

    expect(loadBoards).toHaveBeenCalledTimes(1);
    expect(el.querySelector('mat-progress-bar')).not.toBeNull();
    expect(el.textContent).not.toContain('Board not found');
  });

  it('shows the store error instead of "not found" when loading failed', () => {
    error.set('Boom');
    const el = create('1');

    expect(el.querySelector('[role="alert"]')!.textContent).toContain('Boom');
    expect(el.textContent).not.toContain('Board not found');
  });
});
