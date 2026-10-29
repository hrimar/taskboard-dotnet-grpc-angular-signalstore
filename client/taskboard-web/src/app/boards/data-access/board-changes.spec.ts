import type { Board } from '../../core/proto-gen/board';
import { toBoardChanges } from './board-changes';

describe('toBoardChanges', () => {
  const board = { id: 1, name: 'Alpha', description: 'First' } as Board;

  it('returns null when nothing changed', () => {
    expect(toBoardChanges(board, { name: 'Alpha', description: 'First' })).toBeNull();
  });

  it('returns only the name when only the name changed', () => {
    expect(toBoardChanges(board, { name: 'Beta', description: 'First' })).toEqual({ name: 'Beta' });
  });

  it('returns only the description when only the description changed', () => {
    expect(toBoardChanges(board, { name: 'Alpha', description: 'Second' })).toEqual({ description: 'Second' });
  });

  it('returns both when both changed', () => {
    expect(toBoardChanges(board, { name: 'B', description: 'S' })).toEqual({ name: 'B', description: 'S' });
  });

  it('keeps an emptied description as an explicit empty string so it is cleared', () => {
    expect(toBoardChanges(board, { name: 'Alpha', description: '' })).toEqual({ description: '' });
  });
});
