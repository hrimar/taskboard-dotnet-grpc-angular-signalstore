import { ChangeDetectionStrategy, Component, effect, inject, signal, Signal, untracked } from '@angular/core';
import { AbstractControl, FormControl, NonNullableFormBuilder, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { provideNativeDateAdapter } from '@angular/material/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import type { CreateLabelRequest, Label } from '../../../core/proto-gen/label';
import { TaskItem, TaskItemPriority, TaskItemStatus } from '../../../core/proto-gen/taskitem';
import { dueTimestampToDate } from '../../data-access/due-date';
import type { TaskFormValue } from '../../data-access/task-changes';

// The dialog never injects a store. The host passes in:
//  - `labels`: a live Signal of the labels list (the store's entities), so a label created while
//    the dialog is open shows up in the select without re-opening it;
//  - `createLabel`: a plain callback that forwards the request to the store.
// A callback + live signal was chosen over an output because MatDialog gives the host no template
// to bind outputs in, and over a returned Promise because rxMethod commands return nothing.
export interface TaskFormDialogData {
  task?: TaskItem;
  labels: Signal<Label[]>;
  createLabel: (request: CreateLabelRequest) => void;
}

export type TaskFormResult = TaskFormValue;

// Limits mirror the server's column lengths (TaskItemConfiguration / LabelConfiguration).
export const TASK_TITLE_MAX = 200;
export const TASK_DESCRIPTION_MAX = 2000;
export const LABEL_NAME_MAX = 50;

// Validators.required accepts "   "; check the trimmed value like the board form does.
function notBlank(control: AbstractControl<string>): ValidationErrors | null {
  return control.value.trim().length === 0 ? { required: true } : null;
}

@Component({
  selector: 'app-task-form-dialog',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  // The native adapter is provided here (not app-wide) because only this dialog uses a datepicker.
  providers: [provideNativeDateAdapter()],
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatDatepickerModule
  ],
  templateUrl: './task-form-dialog.component.html',
  styleUrl: './task-form-dialog.component.scss'
})
export class TaskFormDialogComponent {
  private readonly dialogRef = inject<MatDialogRef<TaskFormDialogComponent, TaskFormResult>>(MatDialogRef);
  protected readonly data = inject<TaskFormDialogData>(MAT_DIALOG_DATA);
  private readonly fb = inject(NonNullableFormBuilder);

  protected readonly titleMax = TASK_TITLE_MAX;
  protected readonly descriptionMax = TASK_DESCRIPTION_MAX;
  protected readonly labelNameMax = LABEL_NAME_MAX;

  protected readonly statuses = [
    { value: TaskItemStatus.TODO, text: 'To do' },
    { value: TaskItemStatus.IN_PROGRESS, text: 'In progress' },
    { value: TaskItemStatus.DONE, text: 'Done' }
  ];
  protected readonly priorities = [
    { value: TaskItemPriority.UNSPECIFIED, text: 'None' },
    { value: TaskItemPriority.LOW, text: 'Low' },
    { value: TaskItemPriority.MEDIUM, text: 'Medium' },
    { value: TaskItemPriority.HIGH, text: 'High' }
  ];

  protected readonly form = this.fb.group({
    title: [this.data.task?.title ?? '', [notBlank, Validators.maxLength(TASK_TITLE_MAX)]],
    description: [this.data.task?.description ?? '', [Validators.maxLength(TASK_DESCRIPTION_MAX)]],
    status: [this.data.task?.status ?? TaskItemStatus.TODO],
    priority: [this.data.task?.priority ?? TaskItemPriority.MEDIUM],
    // Nullable on purpose: null means "no due date" (and clears it on edit).
    dueDate: new FormControl<Date | null>(this.data.task?.dueDate ? dueTimestampToDate(this.data.task.dueDate) : null),
    labelIds: [this.data.task ? [...this.data.task.labelIds] : ([] as number[])]
  });

  // Inline "New label" mini-form: separate controls, deliberately NOT part of `form`, so a
  // half-typed label never blocks or pollutes saving the task.
  protected readonly newLabelName = this.fb.control('', [notBlank, Validators.maxLength(LABEL_NAME_MAX)]);
  protected readonly newLabelColor = this.fb.control('#3f51b5');
  protected readonly addingLabel = signal(false);

  // Ids that existed when the user pressed "Add": the first id outside this set is the new label.
  private knownLabelIds: Set<number> | null = null;

  constructor() {
    // createLabel is fire-and-forget (the store's rxMethod returns nothing), so we detect the
    // result by watching the live labels list for an id we have not seen.
    effect(() => {
      const labels = this.data.labels();
      const known = this.knownLabelIds;
      if (!known) return;
      const created = labels.find((label) => !known.has(label.id));
      if (!created) return;
      untracked(() => {
        const control = this.form.controls.labelIds;
        control.setValue([...control.value, created.id]);
        this.newLabelName.reset('');
        this.addingLabel.set(false);
        this.knownLabelIds = null;
      });
    });
  }

  protected addLabel(): void {
    if (this.newLabelName.invalid || this.knownLabelIds) return;
    this.knownLabelIds = new Set(this.data.labels().map((label) => label.id));
    this.data.createLabel({ name: this.newLabelName.value.trim(), color: this.newLabelColor.value });
    this.addingLabel.set(true);
  }

  protected clearDueDate(): void {
    this.form.controls.dueDate.setValue(null);
  }

  protected save(): void {
    if (this.form.invalid) return;
    const value = this.form.getRawValue();
    this.dialogRef.close({ ...value, title: value.title.trim(), description: value.description.trim() });
  }

  protected cancel(): void {
    this.dialogRef.close(undefined);
  }
}
