import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { AppComponent } from './app.component';

describe('AppComponent', () => {
  beforeEach(async () => {
    // The shell no longer talks to the API, so no BOARD_CLIENT fake is needed here.
    await TestBed.configureTestingModule({
      imports: [AppComponent],
      providers: [provideRouter([])]
    }).compileComponents();
  });

  it('should create the app', () => {
    const fixture = TestBed.createComponent(AppComponent);
    expect(fixture.componentInstance).toBeTruthy();
  });

  it('should show a toolbar brand that links to the boards list', () => {
    const fixture = TestBed.createComponent(AppComponent);
    fixture.detectChanges();

    const brand = (fixture.nativeElement as HTMLElement).querySelector('mat-toolbar a.brand');
    expect(brand?.textContent).toContain('TaskBoard');
    expect(brand?.getAttribute('href')).toBe('/boards');
  });

  it('should host the routed page in a router outlet', () => {
    const fixture = TestBed.createComponent(AppComponent);
    fixture.detectChanges();

    expect((fixture.nativeElement as HTMLElement).querySelector('main router-outlet')).not.toBeNull();
  });
});
