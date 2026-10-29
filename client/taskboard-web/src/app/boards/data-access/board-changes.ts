import type { Board, UpdateBoardRequest } from '../../core/proto-gen/board';

export interface BoardFormValue {
  name: string;
  description: string;
}

// UpdateBoardRequest uses proto3 `optional`: a field that is present overwrites the server value,
// an absent one leaves it alone. Sending unchanged fields would needlessly overwrite concurrent
// edits from elsewhere, so only real differences are included. Returns null when nothing changed
// so the caller can skip the round-trip entirely.
export function toBoardChanges(original: Board, edited: BoardFormValue): Omit<UpdateBoardRequest, 'id'> | null {
  const changes: Omit<UpdateBoardRequest, 'id'> = {};
  if (edited.name !== original.name) changes.name = edited.name;
  if (edited.description !== original.description) changes.description = edited.description;
  return Object.keys(changes).length > 0 ? changes : null;
}
