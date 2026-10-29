import { inject } from '@angular/core';
import { patchState, signalStore, withMethods, withState } from '@ngrx/signals';
import { addEntity, setAllEntities, withEntities } from '@ngrx/signals/entities';
import { rxMethod } from '@ngrx/signals/rxjs-interop';
import { tapResponse } from '@ngrx/operators';
import { exhaustMap, from, pipe, switchMap, tap } from 'rxjs';
import { LABEL_CLIENT } from '../../core/grpc/grpc-clients.provider';
import { toErrorMessage } from '../../core/grpc/rpc-error';
import type { CreateLabelRequest, Label } from '../../core/proto-gen/label';

interface LabelsStatus {
  loading: boolean;
  error: string | null;
}

const initialStatus: LabelsStatus = { loading: false, error: null };

// Labels are global (not per board) and few, so one root store holds them all; same shape as BoardsStore.
export const LabelsStore = signalStore(
  { providedIn: 'root' },
  withEntities<Label>(),
  withState<LabelsStatus>(initialStatus),
  withMethods((store, labelClient = inject(LABEL_CLIENT)) => ({
    loadLabels: rxMethod<void>(
      pipe(
        tap(() => patchState(store, { loading: true, error: null })),
        switchMap(() =>
          from(labelClient.listLabels({}).response).pipe(
            tapResponse({
              next: (response) => patchState(store, setAllEntities(response.labels), { loading: false }),
              error: (error: unknown) => patchState(store, { loading: false, error: toErrorMessage(error) })
            })
          )
        )
      )
    ),

    // exhaustMap for the same reason as createBoard: a cancelled client call may still have created the label.
    createLabel: rxMethod<CreateLabelRequest>(
      pipe(
        tap(() => patchState(store, { loading: true, error: null })),
        exhaustMap((request) =>
          from(labelClient.createLabel(request).response).pipe(
            tapResponse({
              next: (label) => patchState(store, addEntity(label), { loading: false }),
              error: (error: unknown) => patchState(store, { loading: false, error: toErrorMessage(error) })
            })
          )
        )
      )
    )
  }))
);
