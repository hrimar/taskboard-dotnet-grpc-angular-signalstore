import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { DatePipe } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import type { Label } from '../../../core/proto-gen/label';
import { TaskItem, TaskItemPriority, TaskItemStatus } from '../../../core/proto-gen/taskitem';
import { dueTimestampToDate, isOverdue } from '../../data-access/due-date';

export const PRIORITY_LABELS: Record<TaskItemPriority, string> = {
  [TaskItemPriority.UNSPECIFIED]: 'No priority',
  [TaskItemPriority.LOW]: 'Low',
  [TaskItemPriority.MEDIUM]: 'Medium',
  [TaskItemPriority.HIGH]: 'High'
};

// Black or white text, whichever is readable on the user-chosen label color (perceived luminance).
function readableTextColor(hex: string): string {
  const match = /^#([0-9a-f]{6})$/i.exec(hex);
  if (!match) return '#000';
  const n = parseInt(match[1], 16);
  const luminance = 0.299 * (n >> 16) + 0.587 * ((n >> 8) & 255) + 0.114 * (n & 255);
  return luminance > 150 ? '#000' : '#fff';
}

// Presentational: everything comes in via inputs, every action goes out via outputs.
@Component({
  selector: 'app-task-card',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe, MatButtonModule, MatIconModule],
  templateUrl: './task-card.component.html',
  styleUrl: './task-card.component.scss'
})
export class TaskCardComponent {
  readonly task = input.required<TaskItem>();
  readonly labelsById = input.required<Record<number, Label>>();

  readonly edit = output<TaskItem>();
  readonly remove = output<TaskItem>();

  protected readonly priorityText = computed(() => PRIORITY_LABELS[this.task().priority]);
  protected readonly priorityClass = computed(() => TaskItemPriority[this.task().priority].toLowerCase());

  // The date is a calendar day (UTC midnight on the wire), so convert before the DatePipe formats it
  // in local time - otherwise it could display the previous day.
  protected readonly dueDate = computed(() => {
    const due = this.task().dueDate;
    return due ? dueTimestampToDate(due) : null;
  });

  // A finished task is never "late".
  protected readonly overdue = computed(
    () => this.task().status !== TaskItemStatus.DONE && isOverdue(this.task().dueDate)
  );

  // Unknown ids (label deleted / not loaded yet) are skipped instead of rendering blanks.
  protected readonly labels = computed(() =>
    this.task()
      .labelIds.map((id) => this.labelsById()[id])
      .filter((label): label is Label => !!label)
      .map((label) => ({ ...label, textColor: readableTextColor(label.color) }))
  );
}
