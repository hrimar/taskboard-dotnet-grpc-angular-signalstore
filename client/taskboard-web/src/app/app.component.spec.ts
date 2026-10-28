import { TestBed } from '@angular/core/testing';
import { AppComponent } from './app.component';
import { BOARD_CLIENT } from './core/grpc/grpc-clients.provider';

describe('AppComponent', () => {
  let fakeBoardClient: { listBoards: jasmine.Spy };

  beforeEach(async () => {
    // AppComponent triggers BoardsStore.loadBoards() on init, so the real gRPC-Web client
    // is replaced with a fake - unit tests must never depend on a running API.
    fakeBoardClient = {
      listBoards: jasmine.createSpy('listBoards').and.returnValue({ response: Promise.resolve({ boards: [] }) })
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

  it('should load boards on init and show the empty state', async () => {
    const fixture = TestBed.createComponent(AppComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(fakeBoardClient.listBoards).toHaveBeenCalledTimes(1);
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('No boards yet.');
  });
});
