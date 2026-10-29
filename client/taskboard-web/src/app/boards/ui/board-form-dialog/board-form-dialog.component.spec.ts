import { TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import type { Board } from '../../../core/proto-gen/board';
import { BoardFormDialogComponent, BoardFormDialogData } from './board-form-dialog.component';

describe('BoardFormDialogComponent', () => {
  let close: jasmine.Spy;

  function create(data: BoardFormDialogData = {}) {
    close = jasmine.createSpy('close');
    TestBed.configureTestingModule({
      imports: [BoardFormDialogComponent],
      providers: [
        provideNoopAnimations(),
        { provide: MatDialogRef, useValue: { close } },
        { provide: MAT_DIALOG_DATA, useValue: data }
      ]
    });
    const fixture = TestBed.createComponent(BoardFormDialogComponent);
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    const nameInput = el.querySelector<HTMLInputElement>('input')!;
    const descInput = el.querySelector<HTMLTextAreaElement>('textarea')!;
    const buttons = Array.from(el.querySelectorAll<HTMLButtonElement>('button'));
    const type = (input: HTMLInputElement | HTMLTextAreaElement, value: string) => {
      input.value = value;
      input.dispatchEvent(new Event('input'));
      fixture.detectChanges();
    };
    return { fixture, el, nameInput, descInput, cancelBtn: buttons[0], saveBtn: buttons[1], type };
  }

  it('shows "New board" and a disabled Save when empty', () => {
    const { el, saveBtn } = create();
    expect(el.querySelector('h2')!.textContent).toContain('New board');
    expect(saveBtn.disabled).toBeTrue();
  });

  it('treats a whitespace-only name as invalid', () => {
    const { saveBtn, type, nameInput } = create();
    type(nameInput, '   ');
    expect(saveBtn.disabled).toBeTrue();
  });

  it('shows a mat-error after the name was touched and left empty', () => {
    const { fixture, el, nameInput } = create();
    nameInput.dispatchEvent(new Event('blur'));
    fixture.detectChanges();
    expect(el.querySelector('mat-error')?.textContent).toContain('Name is required');
  });

  it('rejects a name over 100 and a description over 500 characters', () => {
    const { saveBtn, type, nameInput, descInput } = create();
    type(nameInput, 'a'.repeat(101));
    expect(saveBtn.disabled).toBeTrue();
    type(nameInput, 'a'.repeat(100));
    expect(saveBtn.disabled).toBeFalse();
    type(descInput, 'd'.repeat(501));
    expect(saveBtn.disabled).toBeTrue();
  });

  it('closes with the trimmed value on save', () => {
    const { fixture, type, nameInput, descInput, saveBtn } = create();
    type(nameInput, '  Roadmap  ');
    type(descInput, '  Q1 plans ');
    saveBtn.click();
    fixture.detectChanges();
    expect(close).toHaveBeenCalledOnceWith({ name: 'Roadmap', description: 'Q1 plans' });
  });

  it('closes with undefined on cancel', () => {
    const { cancelBtn } = create();
    cancelBtn.click();
    expect(close).toHaveBeenCalledOnceWith(undefined);
  });

  it('prefills the fields and shows "Edit board" when editing', () => {
    const { el, nameInput, descInput } = create({ board: { id: 3, name: 'Alpha', description: 'First' } as Board });
    expect(el.querySelector('h2')!.textContent).toContain('Edit board');
    expect(nameInput.value).toBe('Alpha');
    expect(descInput.value).toBe('First');
  });
});
