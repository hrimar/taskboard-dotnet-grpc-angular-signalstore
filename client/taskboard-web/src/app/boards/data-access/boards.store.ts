import { inject } from '@angular/core';
import { patchState, signalStore, withMethods, withState } from '@ngrx/signals';
import { setAllEntities, withEntities, addEntity } from '@ngrx/signals/entities';
import { rxMethod } from '@ngrx/signals/rxjs-interop';
import { tapResponse } from '@ngrx/operators';
import { from, pipe, switchMap, tap } from 'rxjs';
import { RpcError } from '@protobuf-ts/runtime-rpc';
import { BOARD_CLIENT } from '../../core/grpc/grpc-clients.provider';
import type { Board, CreateBoardRequest } from '../../core/proto-gen/board';

interface BoardsStatus {
  loading: boolean;
  error: string | null;
}

const initialStatus: BoardsStatus = { loading: false, error: null };

// RpcError (thrown by every @protobuf-ts client call that fails) carries the gRPC
// status message our *GrpcService NotFound/InvalidArgument RpcExceptions set server-side -
// falling back to a generic message for anything else (network failure, CORS, ...).
function toErrorMessage(error: unknown): string {
  return error instanceof RpcError ? error.message : 'Unexpected error calling the API.';
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
    createBoard: rxMethod<CreateBoardRequest>(
      pipe(
        tap(() => patchState(store, { loading: true, error: null })),
        switchMap((request) =>
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
    )
  }))
);
