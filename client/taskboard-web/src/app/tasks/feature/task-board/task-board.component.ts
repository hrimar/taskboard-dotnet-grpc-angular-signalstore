import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { CdkDragDrop, DragDropModule } from '@angular/cdk/drag-drop';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { ConfirmDialogComponent, ConfirmDialogData } from '../../../shared/ui/confirm-dialog/confirm-dialog.component';
import { LabelsStore } from '../../../labels/data-access/labels.store';
import type { Label } from '../../../core/proto-gen/label';
import { TaskItem, TaskItemPriority, TaskItemStatus } from '../../../core/proto-gen/taskitem';
import { toTaskItemUpdate } from '../../data-access/task-changes';
import { dateToDueTimestamp } from '../../data-access/due-date';
import { TasksStore } from '../../data-access/tasks.store';
import { TaskCardComponent } from '../../ui/task-card/task-card.component';
import {
  TaskFormDialogComponent,
  TaskFormDialogData,
  TaskFormResult
} from '../../ui/task-form-dialog/task-form-dialog.component';

@Component({
  selector: 'app-task-board',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DragDropModule, MatButtonModule, MatFormFieldModule, MatInputModule, MatProgressBarModule, MatSelectModule, TaskCardComponent],
  templateUrl: './task-board.component.html',
  styleUrl: './task-board.component.scss'
})
export class TaskBoardComponent {
  readonly boardId = input.required<number>();

  protected readonly tasks = inject(TasksStore);
  protected readonly labels = inject(LabelsStore);
  private readonly dialog = inject(MatDialog);

  protected readonly priorities = [
    { value: TaskItemPriority.HIGH, text: 'High' },
    { value: TaskItemPriority.MEDIUM, text: 'Medium' },
    { value: TaskItemPriority.LOW, text: 'Low' }
  ];

  // data in the cdkDropList is the column's status, so a drop handler knows where a card came from/went to.
  protected readonly columns = [
    { status: TaskItemStatus.TODO, title: 'To do', tasks: this.tasks.todo },
    { status: TaskItemStatus.IN_PROGRESS, title: 'In progress', tasks: this.tasks.inProgress },
    { status: TaskItemStatus.DONE, title: 'Done', tasks: this.tasks.done }
  ];

  protected readonly labelsById = computed(() => {
    const map: Record<number, Label> = {};
    for (const label of this.labels.entities()) map[label.id] = label;
    return map;
  });

  protected reload(): void {
    this.tasks.loadTasks(this.boardId());
    this.labels.loadLabels();
  }

  protected drop(event: CdkDragDrop<TaskItemStatus, TaskItemStatus, TaskItem>): void {
    // Same-column drops are ignored: the contract has no position/order field, so a custom
    // order could not be persisted and would snap back (sorted by id) on the next reload.
    if (event.previousContainer === event.container) return;
    this.tasks.moveTask({ id: event.item.data.id, status: event.container.data });
  }

  protected createTask(): void {
    this.openForm({}).subscribe((result) => {
      if (!result) return;
      this.tasks.createTask({
        boardId: this.boardId(),
        title: result.title,
        // description is a proto3 `optional`: omit it rather than send an empty string.
        ...(result.description ? { description: result.description } : {}),
        status: result.status,
        priority: result.priority,
        ...(result.dueDate ? { dueDate: dateToDueTimestamp(result.dueDate) } : {}),
        labelIds: result.labelIds
      });
    });
  }

  protected editTask(task: TaskItem): void {
    this.openForm({ task }).subscribe((result) => {
      if (!result) return;
      const update = toTaskItemUpdate(task, result);
      if (update) this.tasks.updateTask(update);
    });
  }

  protected deleteTask(task: TaskItem): void {
    this.dialog
      .open<ConfirmDialogComponent, ConfirmDialogData, boolean>(ConfirmDialogComponent, {
        data: {
          title: 'Delete task',
          message: `Delete "${task.title}"? This cannot be undone.`,
          confirmLabel: 'Delete'
        }
      })
      .afterClosed()
      .subscribe((confirmed) => {
        // Backdrop click / Esc yields undefined, so only an explicit true deletes.
        if (confirmed === true) this.tasks.deleteTask({ id: task.id });
      });
  }

  protected setText(event: Event): void {
    this.tasks.setFilter({ text: (event.target as HTMLInputElement).value });
  }

  private openForm(data: Pick<TaskFormDialogData, 'task'>) {
    return this.dialog
      .open<TaskFormDialogComponent, TaskFormDialogData, TaskFormResult>(TaskFormDialogComponent, {
        data: { ...data, labels: this.labels.entities, createLabel: (request) => this.labels.createLabel(request) }
      })
      .afterClosed();
  }
}
