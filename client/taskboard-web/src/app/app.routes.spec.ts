import { Location } from '@angular/common';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { routes } from './app.routes';
import { BoardDetailComponent } from './boards/feature/board-detail/board-detail.component';
import { BoardListComponent } from './boards/feature/board-list/board-list.component';

describe('app routes', () => {
  let harness: RouterTestingHarness;
  let location: Location;

  beforeEach(async () => {
    TestBed.configureTestingModule({ providers: [provideRouter(routes)] });
    harness = await RouterTestingHarness.create();
    location = TestBed.inject(Location);
  });

  it('should redirect the empty path to the boards list', async () => {
    const component = await harness.navigateByUrl('/');

    expect(location.path()).toBe('/boards');
    expect(component).toBeInstanceOf(BoardListComponent);
  });

  it('should lazy-load the detail page for /boards/:id', async () => {
    const component = await harness.navigateByUrl('/boards/5');

    expect(location.path()).toBe('/boards/5');
    expect(component).toBeInstanceOf(BoardDetailComponent);
  });

  it('should send unknown URLs to the boards list instead of a blank page', async () => {
    const component = await harness.navigateByUrl('/no/such/page');

    expect(location.path()).toBe('/boards');
    expect(component).toBeInstanceOf(BoardListComponent);
  });
});
