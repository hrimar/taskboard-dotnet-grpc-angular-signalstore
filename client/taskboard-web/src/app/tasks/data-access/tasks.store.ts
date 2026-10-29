import { computed, inject } from '@angular/core';
import { patchState, signalStore, withComputed, withMethods, withState } from '@ngrx/signals';
import { addEntity, removeEntity, setAllEntities, setEntity, updateEntity, withEntities } from '@ngrx/signals/entities';
import { rxMethod } from '@ngrx/signals/rxjs-interop';
import { tapResponse } from '@ngrx/operators';
import { RpcError } from '@protobuf-ts/runtime-rpc';
import {
  catchError,
  concat,
  concatMap,
  defer,
  EMPTY,
  exhaustMap,
  expand,
  finalize,
  from,
  ignoreElements,
  merge,
  pipe,
  reduce,
  ReplaySubject,
  retry,
  Subject,
  switchMap,
  take,
  tap,
  throwError,
  timer
} from 'rxjs';
import { BOARD_CLIENT, TASK_ITEM_CLIENT } from '../../core/grpc/grpc-clients.provider';
import { toErrorMessage } from '../../core/grpc/rpc-error';
import type { BoardEvent } from '../../core/proto-gen/board';
import {
  CreateTaskItemRequest,
  DeleteTaskItemRequest,
  TaskItem,
  TaskItemPriority,
  TaskItemStatus,
  UpdateTaskItemRequest
} from '../../core/proto-gen/taskitem';
import { watchBoard$ } from './watch-board';

export interface TasksFilter {
  priority: TaskItemPriority | null;
  labelId: number | null;
  text: string;
}

export type WatchConnection = 'idle' | 'connecting' | 'live' | 'reconnecting';

interface TasksState {
  loading: boolean;
  error: string | null;
  // Which board the entities belong to, so switching boards never shows the previous board's cards.
  boardId: number | null;
  // State of the WatchBoard stream; 'idle' also covers "gave up" (board deleted), see watchBoard.
  connection: WatchConnection;
  filter: TasksFilter;
}

const initialState: TasksState = {
  loading: false,
  error: null,
  boardId: null,
  connection: 'idle',
  filter: { priority: null, labelId: null, text: '' }
};

// Large enough that a typical board is one round-trip; the loop below still handles any size.
const PAGE_SIZE = 100;

const RECONNECT_BASE_MS = 1000;
const RECONNECT_MAX_MS = 15000;

// Thrown when the server ends the stream cleanly: a watch is meant to last, so that is also a reason to reconnect.
class WatchEndedError extends Error {}

export interface MoveTaskRequest {
  id: number;
  status: TaskItemStatus;
}

const byId = (a: TaskItem, b: TaskItem) => a.id - b.id;

export const TasksStore = signalStore(
  { providedIn: 'root' },
  withEntities<TaskItem>(),
  withState<TasksState>(initialState),
  withComputed((store) => {
    // Filters apply before grouping, so column counts always reflect what is visible.
    const filtered = computed(() => {
      const { priority, labelId, text } = store.filter();
      const needle = text.trim().toLowerCase();
      return store
        .entities()
        .filter(
          (task) =>
            (priority === null || task.priority === priority) &&
            (labelId === null || task.labelIds.includes(labelId)) &&
            (needle === '' || task.title.toLowerCase().includes(needle) || task.description.toLowerCase().includes(needle))
        );
    });
    // filter() already returns a new array, so sorting in place does not touch the store.
    const column = (status: TaskItemStatus) => computed(() => filtered().filter((t) => t.status === status).sort(byId));

    return {
      todo: column(TaskItemStatus.TODO),
      inProgress: column(TaskItemStatus.IN_PROGRESS),
      done: column(TaskItemStatus.DONE),
      hasActiveFilter: computed(() => {
        const { priority, labelId, text } = store.filter();
        return priority !== null || labelId !== null || text.trim() !== '';
      })
    };
  }),
  withMethods((store, taskClient = inject(TASK_ITEM_CLIENT), boardClient = inject(BOARD_CLIENT)) => {
    // Optimistic edits still waiting for their server response. An incoming WatchBoard event must
    // not undo them (see applyEvent), otherwise the card flickers back and forth.
    const pendingDeletes = new Set<number>();
    const pendingMoves = new Map<number, number>();

    // ListTaskItems is paginated (an empty next_page_token marks the last page), so stopping after
    // the first response would silently drop tasks beyond it.
    // expand() feeds each response back in to fetch the next page, reduce() collects all pages.
    const fetchAllTasks$ = (boardId: number) => {
      const fetchPage = (pageToken: string) =>
        from(taskClient.listTaskItems({ boardId, pageSize: PAGE_SIZE, pageToken }).response);

      return fetchPage('').pipe(
        expand((response) => (response.nextPageToken ? fetchPage(response.nextPageToken) : EMPTY)),
        reduce((all, response) => all.concat(response.taskItems), [] as TaskItem[])
      );
    };

    // Clear on a board switch only: reloading the same board keeps its cards visible while refreshing.
    const enterBoard = (boardId: number) => {
      if (store.boardId() !== boardId) patchState(store, setAllEntities<TaskItem>([]));
      patchState(store, { boardId });
    };

    // Events are upserts/removals, so applying one twice (our own echo) or for an already-gone task is harmless.
    // Decision for the optimistic methods, kept deliberately simple:
    //  - a task with a delete in flight is ignored: the user removed it, resurrecting it would flash it back;
    //  - a task with a move in flight keeps its local status: the event may be the echo of an EARLIER move
    //    and would snap the card back to the old column until the newer echo arrives.
    // An update ignored because of a delete that then fails is lost until the next resync; that is rare and self-healing.
    const applyEvent = (event: BoardEvent) => {
      const change = event.event;
      switch (change.oneofKind) {
        case 'taskCreated':
        case 'taskUpdated': {
          const task = change.oneofKind === 'taskCreated' ? change.taskCreated : change.taskUpdated;
          if (task.boardId !== store.boardId() || pendingDeletes.has(task.id)) return;
          const local = store.entityMap()[task.id];
          patchState(store, setEntity(local && pendingMoves.has(task.id) ? { ...task, status: local.status } : task));
          return;
        }
        case 'taskDeletedId':
          patchState(store, removeEntity(change.taskDeletedId));
          return;
      }
    };

    // One watch session for a board: connect, resync, apply events, reconnect until stopped or NOT_FOUND.
    const watch$ = (boardId: number) => {
      let failures = 0;
      let firstAttempt = true;

      return defer(() => {
        patchState(store, { connection: firstAttempt ? 'connecting' : 'reconnecting' });
        firstAttempt = false;

        const opened$ = new Subject<void>();
        const resynced$ = new ReplaySubject<void>(1);

        // The server sends changes only, no snapshot, and anything that happened while we were
        // disconnected is gone. So after EVERY (re)connect the full list is reloaded.
        const resync$ = opened$.pipe(
          take(1),
          tap(() => {
            failures = 0; // a connection that got established resets the backoff
            patchState(store, { connection: 'live' });
            enterBoard(boardId);
          }),
          switchMap(() =>
            fetchAllTasks$(boardId).pipe(
              tapResponse({
                next: (tasks) => patchState(store, setAllEntities(tasks)),
                error: (error: unknown) => patchState(store, { error: toErrorMessage(error) })
              })
            )
          ),
          // Also after a failed reload: events must not stay blocked forever.
          finalize(() => resynced$.next()),
          ignoreElements()
        );

        // The stream is subscribed BEFORE the reload so no event can slip through the gap.
        // concatMap holds events back until the snapshot is in (it would overwrite them otherwise)
        // and replays them in order, so a stale one is simply followed by the newer one.
        const events$ = concat(
          watchBoard$(boardClient, boardId, () => opened$.next()).pipe(
            concatMap((event) => resynced$.pipe(take(1), tap(() => applyEvent(event)))),
            ignoreElements()
          ),
          throwError(() => new WatchEndedError())
        );

        return merge(resync$, events$);
      }).pipe(
        // defer() + retry() resubscribe, so every retry opens a brand-new call.
        retry({
          delay: (error: unknown) => {
            // Board deleted: reconnecting can never succeed.
            if (error instanceof RpcError && error.code === 'NOT_FOUND') return throwError(() => error);
            patchState(store, { connection: 'reconnecting' });
            const wait = Math.min(RECONNECT_BASE_MS * 2 ** failures, RECONNECT_MAX_MS);
            failures++;
            return timer(wait);
          }
        }),
        catchError((error: unknown) => {
          patchState(store, { error: toErrorMessage(error) });
          return EMPTY;
        }),
        // Also runs on unsubscribe (navigation / board switch), so a stopped watch never looks live.
        finalize(() => patchState(store, { connection: 'idle' }))
      );
    };

    return {
      setFilter(changes: Partial<TasksFilter>): void {
        patchState(store, { filter: { ...store.filter(), ...changes } });
      },

      // Query that walks EVERY page, see fetchAllTasks$.
      loadTasks: rxMethod<number>(
        pipe(
          tap((boardId) => {
            enterBoard(boardId);
            patchState(store, { loading: true, error: null });
          }),
          switchMap((boardId) =>
            fetchAllTasks$(boardId).pipe(
              tapResponse({
                next: (tasks) => patchState(store, setAllEntities(tasks), { loading: false }),
                error: (error: unknown) => patchState(store, { loading: false, error: toErrorMessage(error) })
              })
            )
          )
        )
      ),

      // Pass null to stop: destroying an rxMethod's signal watcher would NOT close a stream that is
      // already running, so stopping is an explicit input the owner sends from its cleanup.
      // Each open watch holds one HTTP/1.1 connection (browsers allow ~6 per origin), so a leak hurts fast.
      watchBoard: rxMethod<number | null>(
        pipe(switchMap((boardId) => (boardId === null ? EMPTY : watch$(boardId))))
      ),

      // exhaustMap, same reasoning as BoardsStore.createBoard.
      createTask: rxMethod<CreateTaskItemRequest>(
        pipe(
          tap(() => patchState(store, { loading: true, error: null })),
          exhaustMap((request) =>
            from(taskClient.createTaskItem(request).response).pipe(
              tapResponse({
                next: (task) => {
                  // The user may have navigated to another board while the call was in flight.
                  if (task.boardId === store.boardId()) patchState(store, addEntity(task));
                  patchState(store, { loading: false });
                },
                error: (error: unknown) => patchState(store, { loading: false, error: toErrorMessage(error) })
              })
            )
          )
        )
      ),

      // Not optimistic: the edit dialog already closed, and the server decides the final values
      // (e.g. label_ids resolved against existing labels), so we wait for its copy.
      // concatMap keeps edits of the same task in order.
      updateTask: rxMethod<UpdateTaskItemRequest>(
        pipe(
          concatMap((request) => {
            patchState(store, { error: null });
            return from(taskClient.updateTaskItem(request).response).pipe(
              tapResponse({
                next: (task) => patchState(store, setEntity(task)),
                error: (error: unknown) => patchState(store, { error: toErrorMessage(error) })
              })
            );
          })
        )
      ),

      // Optimistic status change (drag & drop): the card must land in the new column instantly.
      // Each call snapshots the task when it starts (concatMap runs them in order), so a rollback
      // restores exactly the state before this move and never undoes an earlier successful one.
      moveTask: rxMethod<MoveTaskRequest>(
        pipe(
          concatMap(({ id, status }) => {
            const previous = store.entityMap()[id];
            patchState(store, { error: null });
            if (previous) patchState(store, updateEntity({ id, changes: { status } }));
            pendingMoves.set(id, (pendingMoves.get(id) ?? 0) + 1);

            return from(
              taskClient.updateTaskItem({
                id,
                taskItem: TaskItem.create({ id, status }),
                updateMask: { paths: ['status'] }
              }).response
            ).pipe(
              tapResponse({
                // Only take the server's timestamp: a full setEntity could overwrite a newer
                // optimistic move queued behind this one.
                next: (task) => patchState(store, updateEntity({ id, changes: { updatedAt: task.updatedAt } })),
                error: (error: unknown) => {
                  if (previous) patchState(store, updateEntity({ id, changes: { status: previous.status } }));
                  patchState(store, { error: toErrorMessage(error) });
                }
              }),
              finalize(() => {
                const left = (pendingMoves.get(id) ?? 1) - 1;
                if (left > 0) pendingMoves.set(id, left);
                else pendingMoves.delete(id);
              })
            );
          })
        )
      ),

      // Optimistic delete; the rollback puts the id back at its old index (see BoardsStore.deleteBoard).
      deleteTask: rxMethod<DeleteTaskItemRequest>(
        pipe(
          concatMap((request) => {
            const previous = store.entityMap()[request.id];
            const previousIndex = store.ids().indexOf(request.id);
            patchState(store, { error: null });
            if (previous) patchState(store, removeEntity(request.id));
            pendingDeletes.add(request.id);

            return from(taskClient.deleteTaskItem(request).response).pipe(
              tapResponse({
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
              }),
              finalize(() => pendingDeletes.delete(request.id))
            );
          })
        )
      )
    };
  })
);
