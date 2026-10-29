import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import { MatDialog } from '@angular/material/dialog';
import { of } from 'rxjs';
import { BoardsStore } from '../../data-access/boards.store';
import type { Board } from '../../../core/proto-gen/board';
import { BoardListComponent } from './board-list.component';

describe('BoardListComponent', () => {
  // A signal-backed stub is simpler than the real store + fake client: the component only
  // reads signals and calls store methods, so that is all we need to control.
  const entities = signal<Board[]>([]);
  const loading = signal(false);
  const error = signal<string | null>(null);
  let loadBoards: jasmine.Spy;
  let createBoard: jasmine.Spy;
  let updateBoard: jasmine.Spy;
  let deleteBoard: jasmine.Spy;
  let dialogOpen: jasmine.Spy;
  let fixture: ComponentFixture<BoardListComponent>;

  const el = () => fixture.nativeElement as HTMLElement;
  const render = () => {
    fixture.detectChanges();
  };
  const button = (label: string) =>
    Array.from(el().querySelectorAll<HTMLButtonElement>('button')).find((b) => b.textContent!.trim() === label || b.getAttribute('aria-label') === label)!;
  const dialogResult = (result: unknown) => dialogOpen.and.returnValue({ afterClosed: () => of(result) });

  const alpha = { id: 1, name: 'Alpha', description: 'First board' } as Board;
  const beta = { id: 2, name: 'Beta', description: '' } as Board;

  beforeEach(() => {
    entities.set([]);
    loading.set(false);
    error.set(null);
    loadBoards = jasmine.createSpy('loadBoards');
    createBoard = jasmine.createSpy('createBoard');
    updateBoard = jasmine.createSpy('updateBoard');
    deleteBoard = jasmine.createSpy('deleteBoard');
    dialogOpen = jasmine.createSpy('open');

    TestBed.configureTestingModule({
      imports: [BoardListComponent],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        { provide: BoardsStore, useValue: { entities, loading, error, loadBoards, createBoard, updateBoard, deleteBoard } }
      ]
    });
    // MatDialog is root-provided, so it is overridden here, before the component is created,
    // to avoid opening real overlays.
    TestBed.overrideProvider(MatDialog, { useValue: { open: dialogOpen } });
    fixture = TestBed.createComponent(BoardListComponent);
  });

  it('calls loadBoards once on init', () => {
    render();
    expect(loadBoards).toHaveBeenCalledTimes(1);
  });

  it('renders a card per board whose link goes to /boards/:id and hides empty descriptions', () => {
    entities.set([alpha, beta]);
    render();

    const links = el().querySelectorAll<HTMLAnchorElement>('a.card-link');
    expect(links.length).toBe(2);
    expect(links[0].getAttribute('href')).toBe('/boards/1');
    expect(links[1].getAttribute('href')).toBe('/boards/2');
    expect(links[0].textContent).toContain('Alpha');
    expect(links[0].textContent).toContain('First board');
    expect(links[1].querySelector('mat-card-content')).toBeNull();
  });

  it('keeps the action buttons outside the anchor and labels them with the board name', () => {
    entities.set([alpha]);
    render();

    expect(el().querySelectorAll('a button').length).toBe(0);
    expect(button('Edit board Alpha')).toBeDefined();
    expect(button('Delete board Alpha')).toBeDefined();
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

    button('Retry').click();
    expect(loadBoards).toHaveBeenCalledTimes(2);
  });

  it('keeps the boards visible next to an error from a failed edit or delete', () => {
    entities.set([alpha]);
    error.set('Boom');
    render();
    expect(el().querySelector('[role="alert"]')?.textContent).toContain('Boom');
    expect(el().querySelectorAll('a.card-link').length).toBe(1);
  });

  describe('create', () => {
    it('calls createBoard with name and description', () => {
      dialogResult({ name: 'Gamma', description: 'Third' });
      render();
      button('New board').click();
      expect(createBoard).toHaveBeenCalledOnceWith({ name: 'Gamma', description: 'Third' });
    });

    it('omits an empty description', () => {
      dialogResult({ name: 'Gamma', description: '' });
      render();
      button('New board').click();
      expect(createBoard).toHaveBeenCalledOnceWith({ name: 'Gamma' });
    });

    it('does nothing when the dialog is cancelled', () => {
      dialogResult(undefined);
      render();
      button('New board').click();
      expect(createBoard).not.toHaveBeenCalled();
    });
  });

  describe('edit', () => {
    beforeEach(() => {
      entities.set([alpha]);
    });

    it('opens the dialog with the board', () => {
      dialogResult(undefined);
      render();
      button('Edit board Alpha').click();
      expect(dialogOpen.calls.mostRecent().args[1].data).toEqual({ board: alpha });
    });

    it('does nothing when nothing changed', () => {
      dialogResult({ name: 'Alpha', description: 'First board' });
      render();
      button('Edit board Alpha').click();
      expect(updateBoard).not.toHaveBeenCalled();
    });

    it('does nothing when cancelled', () => {
      dialogResult(undefined);
      render();
      button('Edit board Alpha').click();
      expect(updateBoard).not.toHaveBeenCalled();
    });

    it('sends only the changed fields', () => {
      dialogResult({ name: 'Alpha 2', description: 'First board' });
      render();
      button('Edit board Alpha').click();
      expect(updateBoard).toHaveBeenCalledOnceWith({ id: 1, name: 'Alpha 2' });
    });
  });

  describe('delete', () => {
    beforeEach(() => {
      entities.set([alpha]);
    });

    it('deletes after the user confirms', () => {
      dialogResult(true);
      render();
      button('Delete board Alpha').click();
      expect(deleteBoard).toHaveBeenCalledOnceWith({ id: 1 });
    });

    it('does not delete when the user cancels', () => {
      dialogResult(false);
      render();
      button('Delete board Alpha').click();
      expect(deleteBoard).not.toHaveBeenCalled();
    });

    it('does not delete when the dialog is dismissed without a choice', () => {
      dialogResult(undefined);
      render();
      button('Delete board Alpha').click();
      expect(deleteBoard).not.toHaveBeenCalled();
    });
  });
});
