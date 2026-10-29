import { computed, inject } from '@angular/core';
import { patchState, signalStore, withComputed, withMethods, withState } from '@ngrx/signals';
import { addEntity, removeEntity, setAllEntities, setEntity, updateEntity, withEntities } from '@ngrx/signals/entities';
import { rxMethod } from '@ngrx/signals/rxjs-interop';
import { tapResponse } from '@ngrx/operators';
import { concatMap, EMPTY, exhaustMap, expand, from, pipe, reduce, switchMap, tap } from 'rxjs';
import { TASK_ITEM_CLIENT } from '../../core/grpc/grpc-clients.provider';
import { toErrorMessage } from '../../core/grpc/rpc-error';
import {
  CreateTaskItemRequest,
  DeleteTaskItemRequest,
  TaskItem,
  TaskItemPriority,
  TaskItemStatus,
  UpdateTaskItemRequest
} from '../../core/proto-gen/taskitem';

export interface TasksFilter {
  priority: TaskItemPriority | null;
  labelId: number | null;
  text: string;
}

interface TasksState {
  loading: boolean;
  error: string | null;
  // Which board the entities belong to, so switching boards never shows the previous board's cards.
  boardId: number | null;
  filter: TasksFilter;
}

const initialState: TasksState = {
  loading: false,
  error: null,
  boardId: null,
  filter: { priority: null, labelId: null, text: '' }
};

// Large enough that a typical board is one round-trip; the loop below still handles any size.
const PAGE_SIZE = 100;

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
  withMethods((store, taskClient = inject(TASK_ITEM_CLIENT)) => ({
    setFilter(changes: Partial<TasksFilter>): void {
      patchState(store, { filter: { ...store.filter(), ...changes } });
    },

    // Query that walks EVERY page: ListTaskItems is paginated (an empty next_page_token marks the
    // last page), so stopping after the first response would silently drop tasks beyond it.
    // expand() feeds each response back in to fetch the next page, reduce() collects all pages.
    loadTasks: rxMethod<number>(
      pipe(
        tap((boardId) => {
          // Clear on a board switch only: reloading the same board keeps its cards visible while refreshing.
          if (store.boardId() !== boardId) patchState(store, setAllEntities<TaskItem>([]));
          patchState(store, { boardId, loading: true, error: null });
        }),
        switchMap((boardId) => {
          const fetchPage = (pageToken: string) =>
            from(taskClient.listTaskItems({ boardId, pageSize: PAGE_SIZE, pageToken }).response);

          return fetchPage('').pipe(
            expand((response) => (response.nextPageToken ? fetchPage(response.nextPageToken) : EMPTY)),
            reduce((all, response) => all.concat(response.taskItems), [] as TaskItem[]),
            tapResponse({
              next: (tasks) => patchState(store, setAllEntities(tasks), { loading: false }),
              error: (error: unknown) => patchState(store, { loading: false, error: toErrorMessage(error) })
            })
          );
        })
      )
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
            })
          );
        })
      )
    )
  }))
);
