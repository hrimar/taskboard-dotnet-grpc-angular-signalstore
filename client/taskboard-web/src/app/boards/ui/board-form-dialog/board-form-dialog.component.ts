import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { AbstractControl, NonNullableFormBuilder, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import type { Board } from '../../../core/proto-gen/board';

export interface BoardFormDialogData {
  board?: Board;
}

export interface BoardFormResult {
  name: string;
  description: string;
}

// Limits mirror the server's InvalidArgument rules so the user sees the problem before a round-trip.
export const BOARD_NAME_MAX = 100;
export const BOARD_DESCRIPTION_MAX = 500;

// Validators.required accepts "   "; the server rejects whitespace-only names, so check the trimmed value.
function notBlank(control: AbstractControl<string>): ValidationErrors | null {
  return control.value.trim().length === 0 ? { required: true } : null;
}

// Presentational on purpose: it only returns the form value; the caller decides what to do
// with it (create vs. update), so the dialog never needs the store.
@Component({
  selector: 'app-board-form-dialog',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, MatDialogModule, MatButtonModule, MatFormFieldModule, MatInputModule],
  templateUrl: './board-form-dialog.component.html',
  styleUrl: './board-form-dialog.component.scss'
})
export class BoardFormDialogComponent {
  private readonly dialogRef = inject<MatDialogRef<BoardFormDialogComponent, BoardFormResult>>(MatDialogRef);
  protected readonly data = inject<BoardFormDialogData>(MAT_DIALOG_DATA);

  protected readonly nameMax = BOARD_NAME_MAX;
  protected readonly descriptionMax = BOARD_DESCRIPTION_MAX;

  protected readonly form = inject(NonNullableFormBuilder).group({
    name: [this.data.board?.name ?? '', [notBlank, Validators.maxLength(BOARD_NAME_MAX)]],
    description: [this.data.board?.description ?? '', [Validators.maxLength(BOARD_DESCRIPTION_MAX)]]
  });

  protected save(): void {
    if (this.form.invalid) return;
    const { name, description } = this.form.getRawValue();
    this.dialogRef.close({ name: name.trim(), description: description.trim() });
  }

  protected cancel(): void {
    this.dialogRef.close(undefined);
  }
}
