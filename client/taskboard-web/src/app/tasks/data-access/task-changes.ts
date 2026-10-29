import { TaskItem, TaskItemPriority, TaskItemStatus, UpdateTaskItemRequest } from '../../core/proto-gen/taskitem';
import { dateToDueTimestamp } from './due-date';

export interface TaskFormValue {
  title: string;
  description: string;
  status: TaskItemStatus;
  priority: TaskItemPriority;
  dueDate: Date | null;
  labelIds: number[];
}

// Day-granular on purpose: the due date is a calendar date, so two Dates on the same day are equal.
function sameDueDate(original: TaskItem, edited: Date | null): boolean {
  if (!original.dueDate) return edited === null;
  if (!edited) return false;
  const next = dateToDueTimestamp(edited);
  return original.dueDate.seconds === next.seconds && original.dueDate.nanos === next.nanos;
}

function sameIds(a: number[], b: number[]): boolean {
  const sortedB = [...b].sort((x, y) => x - y);
  return a.length === b.length && [...a].sort((x, y) => x - y).every((id, i) => id === sortedB[i]);
}

// UpdateTaskItem only applies the paths listed in update_mask, so list only real differences
// (sending unchanged fields would overwrite concurrent edits). Returns null when nothing
// changed so the caller can skip the round-trip. Quirk: a due_date path with no due_date in
// the payload CLEARS it on the server - that is how "remove the date" is expressed.
export function toTaskItemUpdate(original: TaskItem, edited: TaskFormValue): UpdateTaskItemRequest | null {
  const paths: string[] = [];
  const taskItem = TaskItem.create({ id: original.id });

  if (edited.title !== original.title) {
    paths.push('title');
    taskItem.title = edited.title;
  }
  if (edited.description !== original.description) {
    paths.push('description');
    taskItem.description = edited.description;
  }
  if (edited.status !== original.status) {
    paths.push('status');
    taskItem.status = edited.status;
  }
  if (edited.priority !== original.priority) {
    paths.push('priority');
    taskItem.priority = edited.priority;
  }
  if (!sameDueDate(original, edited.dueDate)) {
    paths.push('due_date');
    if (edited.dueDate) taskItem.dueDate = dateToDueTimestamp(edited.dueDate);
  }
  if (!sameIds(original.labelIds, edited.labelIds)) {
    paths.push('label_ids');
    taskItem.labelIds = [...edited.labelIds];
  }

  return paths.length > 0 ? { id: original.id, taskItem, updateMask: { paths } } : null;
}
