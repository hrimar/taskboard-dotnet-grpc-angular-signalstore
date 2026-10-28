---
name: signalstore-feature
description: Conventions and templates for NgRx SignalStore (@ngrx/signals 18) stores in the TaskBoard Angular client - entities, loading/error state, rxMethod + tapResponse calls over gRPC-Web, optimistic updates with rollback, and the matching Jasmine spec. Use when creating a store or adding a method to one.
---

# SignalStore feature

Reference implementation: `client/taskboard-web/src/app/boards/data-access/boards.store.ts`. Match its structure and comment style.

## Store skeleton
```ts
export const TasksStore = signalStore(
  { providedIn: 'root' },
  withEntities<TaskItem>(),
  withState<TasksState>(initialState),          // loading, error, filters
  withComputed((store) => ({ /* derived views, e.g. tasks grouped by status */ })),
  withMethods((store, client = inject(TASK_ITEM_CLIENT)) => ({ /* rxMethods */ }))
);
```

## Rules
- **Query** methods: `rxMethod<void | Params>(pipe(tap(set loading), switchMap(...)))` - `switchMap` cancels a stale request when a newer one starts.
- **Command** methods: use `concatMap` when order matters (sequential edits of one entity), `mergeMap` for independent ones; `exhaustMap` to ignore double-clicks on create.
- Every client call: `from(client.method(request).response).pipe(tapResponse({ next, error }))`. Never let an error reach the outer pipe - it completes the `rxMethod` and every later call silently does nothing.
- Errors: `toErrorMessage(error)` (uses `RpcError.message`); always reset `loading` in both branches. It is currently a private function in `boards.store.ts` - when a second store needs it, move it to `core/grpc/rpc-error.ts` and import it from both, rather than copying it.
- Entities: `setAllEntities`, `addEntity`, `updateEntity({ id, changes })`, `removeEntity(id)` from `@ngrx/signals/entities` - no manual array manipulation.
- **Optimistic update** (status change, delete):
  ```ts
  const previous = store.entityMap()[id];
  patchState(store, updateEntity({ id, changes }));        // UI updates instantly
  // ...call the server; in tapResponse.error:
  patchState(store, setEntity(previous), { error: toErrorMessage(e) });  // rollback
  ```
- Filters and sort order live in state; the filtered list is a `withComputed` signal, not a method.
- Server streams (`WatchBoard`): consume `call.responses` as an async iterable wrapped in an Observable, apply each event with entity updaters, unsubscribe on store destroy (`withHooks({ onDestroy })`), reconnect with `retry({ delay })` backoff.

## Spec template (`<feature>.store.spec.ts`)
```ts
const fakeClient = jasmine.createSpyObj<TaskItemServiceClient>('TaskItemServiceClient', ['listTaskItems']);
fakeClient.listTaskItems.and.returnValue({ response: Promise.resolve({ taskItems: [task], nextPageToken: '' }) } as any);

TestBed.configureTestingModule({ providers: [{ provide: TASK_ITEM_CLIENT, useValue: fakeClient }] });
const store = TestBed.inject(TasksStore);
store.loadTasks(boardId);
await new Promise((resolve) => setTimeout(resolve));   // let the client Promise + rxMethod pipe settle
expect(store.entities()).toEqual([task]);
```
Cover: success, error message (`Promise.reject(new RpcError('msg', 'NOT_FOUND'))`), the store still works after an error, and rollback for optimistic methods.
