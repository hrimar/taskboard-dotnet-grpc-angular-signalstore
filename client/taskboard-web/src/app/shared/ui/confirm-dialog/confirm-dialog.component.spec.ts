import { TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { ConfirmDialogComponent } from './confirm-dialog.component';

describe('ConfirmDialogComponent', () => {
  let close: jasmine.Spy;

  function create() {
    close = jasmine.createSpy('close');
    TestBed.configureTestingModule({
      imports: [ConfirmDialogComponent],
      providers: [
        provideNoopAnimations(),
        { provide: MatDialogRef, useValue: { close } },
        { provide: MAT_DIALOG_DATA, useValue: { title: 'Delete board', message: 'Really delete Alpha?', confirmLabel: 'Delete' } }
      ]
    });
    const fixture = TestBed.createComponent(ConfirmDialogComponent);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  const buttons = (el: HTMLElement) => Array.from(el.querySelectorAll<HTMLButtonElement>('button'));

  it('renders title, message and the confirm label', () => {
    const el = create();
    expect(el.textContent).toContain('Delete board');
    expect(el.textContent).toContain('Really delete Alpha?');
    expect(buttons(el)[1].textContent).toContain('Delete');
  });

  it('closes with true on confirm', () => {
    const el = create();
    buttons(el)[1].click();
    expect(close).toHaveBeenCalledWith(true);
  });

  it('closes with false on cancel', () => {
    const el = create();
    buttons(el)[0].click();
    expect(close).toHaveBeenCalledWith(false);
  });
});
