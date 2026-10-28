import { TestBed } from '@angular/core/testing';
import { AppComponent } from './app.component';
import { BOARD_CLIENT } from './core/grpc/grpc-clients.provider';

describe('AppComponent', () => {
  beforeEach(async () => {
    // AppComponent triggers BoardsStore.loadBoards() on init, so the real gRPC-Web client
    // is replaced with a fake - unit tests must never depend on a running API.
    const fakeBoardClient = {
      listBoards: () => ({ response: Promise.resolve({ boards: [] }) })
    };

    await TestBed.configureTestingModule({
      imports: [AppComponent],
      providers: [{ provide: BOARD_CLIENT, useValue: fakeBoardClient }]
    }).compileComponents();
  });

  it('should create the app', () => {
    const fixture = TestBed.createComponent(AppComponent);
    expect(fixture.componentInstance).toBeTruthy();
  });

  it('should render the title', () => {
    const fixture = TestBed.createComponent(AppComponent);
    fixture.detectChanges();
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('h1')?.textContent).toContain('taskboard-web');
  });
});
