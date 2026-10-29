import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import { BoardsStore } from '../../data-access/boards.store';
import type { Board } from '../../../core/proto-gen/board';
import { BoardListComponent } from './board-list.component';

describe('BoardListComponent', () => {
  // A signal-backed stub is simpler than the real store + fake client: the component only
  // reads signals and calls loadBoards, so that is all we need to control.
  const entities = signal<Board[]>([]);
  const loading = signal(false);
  const error = signal<string | null>(null);
  let loadBoards: jasmine.Spy;
  let fixture: ComponentFixture<BoardListComponent>;

  const el = () => fixture.nativeElement as HTMLElement;
  const render = () => {
    fixture.detectChanges();
  };

  beforeEach(() => {
    entities.set([]);
    loading.set(false);
    error.set(null);
    loadBoards = jasmine.createSpy('loadBoards');

    TestBed.configureTestingModule({
      imports: [BoardListComponent],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        { provide: BoardsStore, useValue: { entities, loading, error, loadBoards } }
      ]
    });
    fixture = TestBed.createComponent(BoardListComponent);
  });

  it('calls loadBoards once on init', () => {
    render();
    expect(loadBoards).toHaveBeenCalledTimes(1);
  });

  it('renders a card per board linking to /boards/:id and hides empty descriptions', () => {
    entities.set([
      { id: 1, name: 'Alpha', description: 'First board' } as Board,
      { id: 2, name: 'Beta', description: '' } as Board
    ]);
    render();

    const links = el().querySelectorAll<HTMLAnchorElement>('a.card-link');
    expect(links.length).toBe(2);
    expect(links[0].getAttribute('href')).toBe('/boards/1');
    expect(links[1].getAttribute('href')).toBe('/boards/2');
    expect(links[0].textContent).toContain('Alpha');
    expect(links[0].textContent).toContain('First board');
    expect(links[1].querySelector('mat-card-content')).toBeNull();
  });

  it('shows the empty state', () => {
    render();
    expect(el().textContent).toContain('No boards yet');
  });

  it('shows the loading bar only while loading with no boards', () => {
    loading.set(true);
    render();
    expect(el().querySelector('mat-progress-bar')).not.toBeNull();
    expect(el().textContent).not.toContain('No boards yet');
  });

  it('keeps showing cached boards while refreshing', () => {
    loading.set(true);
    entities.set([{ id: 1, name: 'Alpha', description: '' } as Board]);
    render();
    expect(el().querySelector('mat-progress-bar')).toBeNull();
    expect(el().querySelectorAll('a.card-link').length).toBe(1);
  });

  it('shows the error and Retry calls loadBoards again', () => {
    error.set('Boom');
    render();
    expect(el().querySelector('[role="alert"]')?.textContent).toContain('Boom');

    el().querySelector<HTMLButtonElement>('button')!.click();
    expect(loadBoards).toHaveBeenCalledTimes(2);
  });
});
