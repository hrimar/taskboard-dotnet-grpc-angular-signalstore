import { inject } from '@angular/core';
import { patchState, signalStore, withMethods, withState } from '@ngrx/signals';
import { setAllEntities, setEntity, updateEntity, removeEntity, withEntities, addEntity } from '@ngrx/signals/entities';
import { rxMethod } from '@ngrx/signals/rxjs-interop';
import { tapResponse } from '@ngrx/operators';
import { concatMap, exhaustMap, from, pipe, switchMap, tap } from 'rxjs';
import { BOARD_CLIENT } from '../../core/grpc/grpc-clients.provider';
import { toErrorMessage } from '../../core/grpc/rpc-error';
import type { Board, CreateBoardRequest, DeleteBoardRequest, UpdateBoardRequest } from '../../core/proto-gen/board';

interface BoardsStatus {
  loading: boolean;
  error: string | null;
}

const initialStatus: BoardsStatus = { loading: false, error: null };

// Only the fields the caller actually set. UpdateBoardRequest uses proto3 `optional`, where an
// unset field means "leave unchanged" - copying `undefined` over the entity would blank it.
function toChanges(request: UpdateBoardRequest): Partial<Board> {
  const changes: Partial<Board> = {};
  if (request.name !== undefined) changes.name = request.name;
  if (request.description !== undefined) changes.description = request.description;
  return changes;
}

// withEntities stores `boards` as a normalized { ids: number[], entityMap: Record<number, Board> }
// instead of a plain array - lookups by id are O(1), and every entity method below
// (setAllEntities, addEntity, ...) comes from @ngrx/signals/entities instead of us writing
// array splice/map logic by hand. Board.id (number) is used as the key automatically.
export const BoardsStore = signalStore(
  { providedIn: 'root' },
  withEntities<Board>(),
  withState<BoardsStatus>(initialStatus),
  withMethods((store, boardClient = inject(BOARD_CLIENT)) => ({
    // Query: rxMethod<void> because ListBoards currently takes no filters - calling
    // loadBoards() re-fetches and replaces the whole entity collection.
    loadBoards: rxMethod<void>(
      pipe(
        tap(() => patchState(store, { loading: true, error: null })),
        switchMap(() =>
          // boardClient.listBoards(...).response is a Promise, not an Observable -
          // from() adapts it so it can live inside an RxJS pipe.
          from(boardClient.listBoards({}).response).pipe(
            tapResponse({
              // tapResponse (not a plain .subscribe) matters here: a plain error would
              // propagate through switchMap and kill the whole rxMethod stream, so a
              // second loadBoards() call afterwards would silently do nothing. tapResponse
              // turns the error into a side effect instead, keeping the method reusable.
              next: (response) => patchState(store, setAllEntities(response.boards), { loading: false }),
              error: (error: unknown) => patchState(store, { loading: false, error: toErrorMessage(error) })
            })
          )
        )
      )
    ),

    // Command: takes a value (the new board's data) rather than void.
    // exhaustMap, not switchMap: switchMap would cancel the first create on the client when a
    // second one arrives, yet the server has already received (and persists) it - the board
    // would exist but never show up in the list. exhaustMap ignores a new create while one
    // is running, so what the user sees always matches what the server did.
    createBoard: rxMethod<CreateBoardRequest>(
      pipe(
        tap(() => patchState(store, { loading: true, error: null })),
        exhaustMap((request) =>
          from(boardClient.createBoard(request).response).pipe(
            tapResponse({
              // No second ListBoards round-trip: the server's response already *is*
              // the created Board (with its generated id), so we just add it locally.
              next: (board) => patchState(store, addEntity(board), { loading: false }),
              error: (error: unknown) => patchState(store, { loading: false, error: toErrorMessage(error) })
            })
          )
        )
      )
    ),

    // Optimistic command: the change shows up immediately, then is confirmed or rolled back.
    // No `loading` flag - the UI should feel instant, not flash a spinner for every edit.
    // concatMap (not switchMap/exhaustMap): every edit must reach the server, in order, and
    // each one snapshots the board *when it starts*, so a rollback never undoes an earlier
    // edit that already succeeded.
    updateBoard: rxMethod<UpdateBoardRequest>(
      pipe(
        concatMap((request) => {
          const previous = store.entityMap()[request.id];
          patchState(store, { error: null });
          if (previous) {
            patchState(store, updateEntity({ id: request.id, changes: toChanges(request) }));
          }

          return from(boardClient.updateBoard(request).response).pipe(
            tapResponse({
              // The server's copy is the truth (it may normalize values), so replace the guess.
              next: (board) => patchState(store, setEntity(board)),
              error: (error: unknown) => {
                if (previous) patchState(store, setEntity(previous));
                patchState(store, { error: toErrorMessage(error) });
              }
            })
          );
        })
      )
    ),

    // Optimistic delete, same reasoning as updateBoard (concatMap, no loading flag).
    // The extra twist is the rollback: setEntity alone would append the board to the end of
    // `ids`, so a failed delete would make it jump to the bottom of the list. The original
    // index is remembered and the id is spliced back to that spot.
    deleteBoard: rxMethod<DeleteBoardRequest>(
      pipe(
        concatMap((request) => {
          const previous = store.entityMap()[request.id];
          const previousIndex = store.ids().indexOf(request.id);
          patchState(store, { error: null });
          if (previous) {
            patchState(store, removeEntity(request.id));
          }

          return from(boardClient.deleteBoard(request).response).pipe(
            tapResponse({
              // Nothing to do on success - the board is already gone from the list.
              next: () => undefined,
              error: (error: unknown) => {
                if (previous) {
                  patchState(store, setEntity(previous));
                  const ids = store.ids().filter((id) => id !== request.id);
                  ids.splice(Math.min(previousIndex, ids.length), 0, request.id);
                  patchState(store, { ids });
                }
                patchState(store, { error: toErrorMessage(error) });
              }
            })
          );
        })
      )
    )
  }))
);
