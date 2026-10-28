import { TestBed } from '@angular/core/testing';
import { RpcError } from '@protobuf-ts/runtime-rpc';
import { BOARD_CLIENT } from '../../core/grpc/grpc-clients.provider';
import type { Board } from '../../core/proto-gen/board';
import { BoardsStore } from './boards.store';

describe('BoardsStore', () => {
  let fakeClient: {
    listBoards: jasmine.Spy;
    createBoard: jasmine.Spy;
    updateBoard: jasmine.Spy;
    deleteBoard: jasmine.Spy;
  };

  const board = (id: number, name: string): Board => ({ id, name, description: '' });

  // The client calls return a protobuf-ts UnaryCall; the store only touches `.response`,
  // so a plain object with a Promise is a faithful stand-in.
  const ok = <T>(value: T) => ({ response: Promise.resolve(value) });
  const fail = (message: string) => ({ response: Promise.reject(new RpcError(message, 'NOT_FOUND')) });

  // Lets the client Promise and the rxMethod pipe settle (macrotask runs after all microtasks).
  const settle = () => new Promise<void>((resolve) => setTimeout(resolve));

  beforeEach(() => {
    fakeClient = {
      listBoards: jasmine.createSpy('listBoards'),
      createBoard: jasmine.createSpy('createBoard'),
      updateBoard: jasmine.createSpy('updateBoard'),
      deleteBoard: jasmine.createSpy('deleteBoard')
    };
    TestBed.configureTestingModule({ providers: [{ provide: BOARD_CLIENT, useValue: fakeClient }] });
  });

  it('should start empty, idle and without an error', () => {
    const store = TestBed.inject(BoardsStore);

    expect(store.entities()).toEqual([]);
    expect(store.loading()).toBeFalse();
    expect(store.error()).toBeNull();
  });

  it('should populate entities and clear loading when boards load', async () => {
    const boards = [board(1, 'Alpha'), board(2, 'Beta')];
    fakeClient.listBoards.and.returnValue(ok({ boards }));
    const store = TestBed.inject(BoardsStore);

    store.loadBoards();
    // The loading flag is set synchronously, before the Promise resolves.
    expect(store.loading()).toBeTrue();
    await settle();

    expect(store.entities()).toEqual(boards);
    expect(store.loading()).toBeFalse();
    expect(store.error()).toBeNull();
  });

  it('should set the RpcError message on failure and keep working for a later call', async () => {
    fakeClient.listBoards.and.returnValues(fail('Board not found'), ok({ boards: [board(1, 'Alpha')] }));
    const store = TestBed.inject(BoardsStore);

    store.loadBoards();
    await settle();

    expect(store.error()).toBe('Board not found');
    expect(store.loading()).toBeFalse();
    expect(store.entities()).toEqual([]);

    // If the error had leaked into the outer pipe, the rxMethod would be completed
    // and this second call would silently do nothing (tapResponse prevents that).
    store.loadBoards();
    await settle();

    expect(fakeClient.listBoards).toHaveBeenCalledTimes(2);
    expect(store.entities()).toEqual([board(1, 'Alpha')]);
    expect(store.error()).toBeNull();
  });

  it('should use a generic message for non-RPC errors', async () => {
    fakeClient.listBoards.and.returnValue({ response: Promise.reject(new TypeError('network down')) });
    const store = TestBed.inject(BoardsStore);

    store.loadBoards();
    await settle();

    expect(store.error()).toBe('Unexpected error calling the API.');
  });

  it('should add the created board without refetching the list', async () => {
    const created = board(7, 'New');
    fakeClient.createBoard.and.returnValue(ok(created));
    const store = TestBed.inject(BoardsStore);

    store.createBoard({ name: 'New' });
    await settle();

    expect(fakeClient.createBoard).toHaveBeenCalledWith({ name: 'New' });
    expect(fakeClient.listBoards).not.toHaveBeenCalled();
    expect(store.entities()).toEqual([created]);
    expect(store.loading()).toBeFalse();
  });

  it('should ignore a second create while the first is in flight instead of dropping the first', async () => {
    // Held open by hand so the second call arrives while the first is still pending.
    let resolveFirst!: (value: Board) => void;
    const first = board(1, 'First');
    fakeClient.createBoard.and.returnValues(
      { response: new Promise<Board>((resolve) => (resolveFirst = resolve)) },
      ok(board(2, 'Second'))
    );
    const store = TestBed.inject(BoardsStore);

    store.createBoard({ name: 'First' });
    store.createBoard({ name: 'Second' });
    resolveFirst(first);
    await settle();

    // With switchMap the first response would be discarded and the server called twice -
    // the first board would exist on the server but never reach the list.
    expect(fakeClient.createBoard).toHaveBeenCalledTimes(1);
    expect(store.entities()).toEqual([first]);
  });

  it('should set the error and add nothing when create fails', async () => {
    fakeClient.createBoard.and.returnValue(fail('Name is required'));
    const store = TestBed.inject(BoardsStore);

    store.createBoard({ name: '' });
    await settle();

    expect(store.error()).toBe('Name is required');
    expect(store.entities()).toEqual([]);
    expect(store.loading()).toBeFalse();
  });

  describe('updateBoard', () => {
    // Loads two boards so each test starts from a known list.
    const loadedStore = async () => {
      fakeClient.listBoards.and.returnValue(ok({ boards: [board(1, 'Alpha'), board(2, 'Beta')] }));
      const store = TestBed.inject(BoardsStore);
      store.loadBoards();
      await settle();
      return store;
    };

    it('should show the change immediately, before the server answers', async () => {
      const store = await loadedStore();
      fakeClient.updateBoard.and.returnValue({ response: new Promise<Board>(() => {}) });

      store.updateBoard({ id: 1, name: 'Renamed' });

      expect(store.entityMap()[1].name).toBe('Renamed');
      // Optimistic edits must not flash the list-level spinner.
      expect(store.loading()).toBeFalse();
    });

    it('should replace the optimistic guess with the server copy on success', async () => {
      const store = await loadedStore();
      fakeClient.updateBoard.and.returnValue(ok(board(1, 'Renamed by server')));

      store.updateBoard({ id: 1, name: 'Renamed' });
      await settle();

      expect(store.entityMap()[1].name).toBe('Renamed by server');
      expect(store.error()).toBeNull();
    });

    it('should roll back to the previous board and set the error when the update fails', async () => {
      const store = await loadedStore();
      fakeClient.updateBoard.and.returnValue(fail('Board 1 not found.'));

      store.updateBoard({ id: 1, name: 'Renamed' });
      expect(store.entityMap()[1].name).toBe('Renamed');
      await settle();

      expect(store.entityMap()[1]).toEqual(board(1, 'Alpha'));
      expect(store.error()).toBe('Board 1 not found.');
    });

    it('should leave fields the request did not set untouched', async () => {
      fakeClient.listBoards.and.returnValue(ok({ boards: [{ ...board(1, 'Alpha'), description: 'keep me' }] }));
      const store = TestBed.inject(BoardsStore);
      store.loadBoards();
      await settle();
      fakeClient.updateBoard.and.returnValue({ response: new Promise<Board>(() => {}) });

      store.updateBoard({ id: 1, name: 'Renamed' });

      expect(store.entityMap()[1].description).toBe('keep me');
    });

    it('should still call the server and surface the error for a board it does not know', async () => {
      const store = await loadedStore();
      fakeClient.updateBoard.and.returnValue(fail('Board 99 not found.'));

      store.updateBoard({ id: 99, name: 'Ghost' });
      await settle();

      expect(fakeClient.updateBoard).toHaveBeenCalledWith({ id: 99, name: 'Ghost' });
      expect(store.entities().length).toBe(2);
      expect(store.error()).toBe('Board 99 not found.');
    });

    it('should keep the store usable after a failed update', async () => {
      const store = await loadedStore();
      fakeClient.updateBoard.and.returnValues(fail('boom'), ok(board(1, 'Second try')));

      store.updateBoard({ id: 1, name: 'First try' });
      await settle();
      store.updateBoard({ id: 1, name: 'Second try' });
      await settle();

      expect(store.entityMap()[1].name).toBe('Second try');
      expect(store.error()).toBeNull();
    });
  });

  describe('deleteBoard', () => {
    const loadedStore = async () => {
      fakeClient.listBoards.and.returnValue(
        ok({ boards: [board(1, 'Alpha'), board(2, 'Beta'), board(3, 'Gamma')] })
      );
      const store = TestBed.inject(BoardsStore);
      store.loadBoards();
      await settle();
      return store;
    };

    it('should remove the board immediately, before the server answers', async () => {
      const store = await loadedStore();
      fakeClient.deleteBoard.and.returnValue({ response: new Promise(() => {}) });

      store.deleteBoard({ id: 2 });

      expect(store.entities().map((b) => b.id)).toEqual([1, 3]);
      expect(store.loading()).toBeFalse();
    });

    it('should keep the board removed once the server confirms', async () => {
      const store = await loadedStore();
      fakeClient.deleteBoard.and.returnValue(ok({}));

      store.deleteBoard({ id: 2 });
      await settle();

      expect(fakeClient.deleteBoard).toHaveBeenCalledWith({ id: 2 });
      expect(store.entities().map((b) => b.id)).toEqual([1, 3]);
      expect(store.error()).toBeNull();
    });

    it('should put the board back at its original position and set the error on failure', async () => {
      const store = await loadedStore();
      fakeClient.deleteBoard.and.returnValue(fail('Board 2 not found.'));

      store.deleteBoard({ id: 2 });
      expect(store.entities().map((b) => b.id)).toEqual([1, 3]);
      await settle();

      // Middle position, not appended at the end - the user's list must look unchanged.
      expect(store.entities().map((b) => b.id)).toEqual([1, 2, 3]);
      expect(store.entityMap()[2]).toEqual(board(2, 'Beta'));
      expect(store.error()).toBe('Board 2 not found.');
    });

    it('should restore the first and the last board to their own positions too', async () => {
      const store = await loadedStore();
      fakeClient.deleteBoard.and.returnValues(fail('x'), fail('y'));

      store.deleteBoard({ id: 1 });
      await settle();
      store.deleteBoard({ id: 3 });
      await settle();

      expect(store.entities().map((b) => b.id)).toEqual([1, 2, 3]);
    });

    it('should still call the server and surface the error for a board it does not know', async () => {
      const store = await loadedStore();
      fakeClient.deleteBoard.and.returnValue(fail('Board 99 not found.'));

      store.deleteBoard({ id: 99 });
      await settle();

      expect(fakeClient.deleteBoard).toHaveBeenCalledWith({ id: 99 });
      expect(store.entities().length).toBe(3);
      expect(store.error()).toBe('Board 99 not found.');
    });
  });
});
